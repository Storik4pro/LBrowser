using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Profile;
using Windows.UI.Xaml.Controls;

namespace LinesBrowser.Network
{
    public class JitterBuffer
    {
        private readonly SortedDictionary<long, byte[]> _buf = new SortedDictionary<long, byte[]>();
        private readonly object _lock = new object();
        private readonly int _maxBufferMs;
        public double PlayoutStartUs { get; private set; } = 0;
        public long PlayoutOffsetTicks => (long)Math.Round((CurrentUs - PlayoutStartUs) * 10);

        private long CurrentUs => (long)(DateTime.UtcNow - _t0).TotalMilliseconds * 1000;
        private readonly DateTime _t0 = DateTime.UtcNow;
        public int _bytesPerFrame;

        public JitterBuffer(int maxBufferMs = 200) { _maxBufferMs = maxBufferMs; }

        public int BufferLengthMs
        {
            get
            {
                lock (_lock)
                {
                    if (_buf.Count < 2) return 0;
                    var keys = _buf.Keys;
                    return (int)((keys.Max() - keys.Min()) / 1000);
                }
            }
        }

        public int FrameCount
        {
            get { lock (_lock) return _buf.Values.Count(b => b.Length >= _bytesPerFrame); }
        }

        public void AddFrame(long ptsUs, byte[] data)
        {
            lock (_lock)
            {
                if (!_buf.ContainsKey(ptsUs)) _buf[ptsUs] = data;
                if (PlayoutStartUs == 0 && _buf.Count > 0)
                    PlayoutStartUs = _buf.Keys.First() + _maxBufferMs * 1000;
            }
        }

        public byte[] GetFrame(long playbackUs)
        {
            lock (_lock)
            {
                var tooOld = _buf.Keys.Where(k => k < playbackUs - _maxBufferMs * 1000).ToList();
                foreach (var k in tooOld) _buf.Remove(k);

                var key = _buf.Keys.FirstOrDefault(k => k <= playbackUs);
                if (key != 0)
                {
                    var data = _buf[key];
                    _buf.Remove(key);
                    return data;
                }
                return null;
            }
        }
        public Tuple<byte[], long> GetOldestFrame()
        {
            lock (_lock)
            {
                if (_buf.Count == 0) return null;
                var firstKey = _buf.Keys.First();
                var lastKey = _buf.Keys.Last();
                Debug.WriteLine($"[AUDIO] now played packet time (Us) {firstKey}, last get from server packet time (Us) {lastKey} => difference {lastKey - firstKey}");

                if (lastKey - firstKey > 1_000_000)
                    _buf.Remove(firstKey);
                firstKey = _buf.Keys.First();

                var data = _buf[firstKey];
                _buf.Remove(firstKey);
                return Tuple.Create(data, firstKey);
            }
        }
    }
    public class AudioStreamerClient : IDisposable
    {
        private MediaPlayerElement _playerElement;
        private MediaPlayer _mediaPlayer;
        private MediaStreamSource _mss;
        private MessageWebSocket _msgWebSocket;
        private double _nextTimestampUs = 0;

        private const int SampleRate = 16000;
        private const int ChannelCount = 2;
        private const int BitsPerSample = 32;
        private const int BytesPerSample = sizeof(float);
        private readonly int _bytesPerFrame = BytesPerSample * ChannelCount;

        private readonly JitterBuffer _jitter = new JitterBuffer(200);
        private long _lastPtcUs;

        public EventHandler<Tuple<bool, string>> ServerConnected;

        private static ResourceLoader resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

        public AudioStreamerClient()
        {
            _jitter._bytesPerFrame = _bytesPerFrame;
            _mediaPlayer = new MediaPlayer { AutoPlay = false };
            _mediaPlayer.RealTimePlayback = true;
            var props = AudioEncodingProperties.CreatePcm((uint)SampleRate, (uint)ChannelCount, (uint)BitsPerSample);
            props.Subtype = "Float";

            bool isWindowsMobile = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile";
            if (isWindowsMobile)
            {
                _mss = new MediaStreamSource(new AudioStreamDescriptor(props));
            }
            else
            {
                _mss = new MediaStreamSource(new AudioStreamDescriptor(props))
                {
                    BufferTime = TimeSpan.FromMilliseconds(50),
                    IsLive = true
                };
            }
            _mss.SampleRequested += OnSampleRequested;
            _mediaPlayer.Source = MediaSource.CreateFromMediaStreamSource(_mss);

        }

        private string HandleError(Exception ex)
        {
            var codeEnum = ErrorHelper.MapExceptionToCode(ex, out uint? hr);
            string codeString;

            if (codeEnum.HasValue)
            {
                codeString = $"ERR_AUDIO_{codeEnum.Value.ToString().ToUpperInvariant()}";
            }
            else if (hr.HasValue)
            {
                string hrHex = $"0x{hr.Value:X8}";
                codeString = $"ERR_AUDIO_CODE_{hrHex}";
            }
            else
            {
                codeString = "ERR_AUDIO_UNKNOWN";
            }
            return codeString;
        }

        public async Task StartAsync(string serverUrl)
        {
            try
            {
                _msgWebSocket = new MessageWebSocket();
                _msgWebSocket.Control.MessageType = SocketMessageType.Binary;
                _msgWebSocket.MessageReceived += OnMessageReceived;
                await _msgWebSocket.ConnectAsync(new Uri(serverUrl));
                ServerConnected?.Invoke(this, Tuple.Create(true, ""));

                for (int i = 0; i < 3; i++)
                    _jitter.AddFrame(0, new byte[_bytesPerFrame]);

                while (_jitter.FrameCount < 3)
                    await Task.Delay(1);
                _mediaPlayer.Play();

            }
            catch (Exception ex)
            {
                string codeString = HandleError(ex);
                ServerConnected?.Invoke(this, Tuple.Create(false, codeString));

                // await ShowErrorDialogAsync("Audio Stream Error", codeString);

            }
        }

        private void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                var reader = args.GetDataReader();
                reader.ByteOrder = ByteOrder.LittleEndian;

                if (reader.UnconsumedBufferLength < 12) return;
                int seq = reader.ReadInt32();
                long pts = reader.ReadInt64();

                uint payloadLen = reader.UnconsumedBufferLength;
                byte[] buf = new byte[payloadLen];
                reader.ReadBytes(buf);

                var msIn = new MemoryStream(buf);
                var gzip = new GZipStream(msIn, CompressionMode.Decompress);
                var msOut = new MemoryStream();

                gzip.CopyTo(msOut);
                var pcm = msOut.ToArray();
                _jitter.AddFrame(pts, pcm);

            }
            catch (Exception ex)
            {
                string codeString = HandleError(ex);

                _ = ShowErrorDialogAsync(
                    "AudioDisconnect",
                    "AudioDisconnectMessage",
                    $"{codeString}"
                    );
                Stop();
            }
        }

        private void OnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            byte[] frame;
            long ptsUs;
            var tuple = _jitter.GetOldestFrame();
            if (tuple == null)
            {
                frame = new byte[_bytesPerFrame];
                ptsUs = 0;
            }
            else
            {
                frame = tuple.Item1;
                ptsUs = tuple.Item2;
            }

            int sampleCount = frame.Length / _bytesPerFrame;
            double durationUs = sampleCount / (double)SampleRate * 1_000_000;

            TimeSpan timestamp;

            if (ptsUs == 0)
                timestamp = TimeSpan.FromTicks((long)Math.Round(_nextTimestampUs * 10));
            else
                timestamp = TimeSpan.FromTicks(ptsUs);
            var buffer = frame.AsBuffer();
            var sample = MediaStreamSample.CreateFromBuffer(buffer, timestamp);
            sample.Duration = TimeSpan.FromTicks((long)Math.Round(durationUs * 10));

            args.Request.Sample = sample;

            _nextTimestampUs += durationUs;

        }

        public void Stop()
        {
            _msgWebSocket?.Dispose();
            _mediaPlayer.Pause();
            _mediaPlayer.Source = null;
        }

        public void Dispose() => Stop();
        private async Task ShowErrorDialogAsync(string titleKey, string messageKey, string message)
        {
            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView?.CoreWindow?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        var dialog = new Windows.UI.Xaml.Controls.ContentDialog
                        {
                            Title = resourceLoader.GetString(titleKey),
                            Content = resourceLoader.GetString(messageKey).Replace("<br>", "\n") + $"\n{message}",
                            CloseButtonText = "OK"
                        };
                        await dialog.ShowAsync();
                    });
            }
            else
            {
                Debug.WriteLine($"ShowErrorDialogAsync fallback: {titleKey} - {messageKey}");
            }
        }
    }
}
