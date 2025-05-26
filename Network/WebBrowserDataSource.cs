using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using Windows.UI.Input;
using Windows.Foundation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Windows.Storage;
using System.IO;
using Windows.UI.Xaml.Controls;
using System.Collections.Concurrent;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Networking.Sockets;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using System.Data;
using Windows.Networking;
using Windows.System.Profile;
using Windows.ApplicationModel.Resources;
using System.ServiceModel.Channels;
using System.IO.Compression;

namespace LinesBrowser
{
    
    public class WebBrowserDataSource : IDisposable
    {
        ClientWebSocket sock;
        public event EventHandler<string> JSONReceived;
        public event EventHandler<BitmapImage> FrameReceived;
        public event EventHandler<TextPacket> TextPacketReceived;
        public event EventHandler<BitmapImage> FullPageScreenshotReceived;
        public event EventHandler<string> ErrorHappensReceived;
        public event EventHandler<bool> ServerSendComplete;
        public event EventHandler<long> TabImageSendComplete;
        public event EventHandler<Tuple<bool, string>> ServerConnectComplete;

        public bool isServerLoadingComplete = false;

        public bool StaticUpdateMode = false;
        private Dictionary<string, Dictionary<int, string>> _imageChunks = new Dictionary<string, Dictionary<int, string>>();
        private Dictionary<string, int> _expectedChunks = new Dictionary<string, int>();

        private bool isDisconnectReasonByUser = false;

        private string HandleError(Exception ex)
        {
            var codeEnum = ErrorHelper.MapExceptionToCode(ex, out uint? hr);
            string codeString;

            if (codeEnum.HasValue)
            {
                codeString = $"ERR_NET_{codeEnum.Value.ToString().ToUpperInvariant()}";
            }
            else if (hr.HasValue)
            {
                string hrHex = $"0x{hr.Value:X8}";
                codeString = $"ERR_NET_CODE_{hrHex}";
            }
            else
            {
                codeString = "ERR_NET_UNKNOWN";
            }
            return codeString;
        } 

        public async void StartReceive(string addr)
        {
            isDisconnectReasonByUser = false;
            sock = new ClientWebSocket();
            try
            {
                await sock.ConnectAsync(new Uri(addr), CancellationToken.None);
                ServerConnectComplete?.Invoke(this, Tuple.Create(true, ""));
            }
            catch (Exception ex)
            {
                string codeString = HandleError(ex);
                ServerConnectComplete?.Invoke(this, Tuple.Create(false, codeString));
                return;
            }

            ArraySegment<byte> readbuffer = new ArraySegment<byte>(new byte[2000000]);
            


            while (sock.State == WebSocketState.Open)
            {
                WebSocketReceiveResult res;
                byte[] actualData;

                try
                {
                    Array.Clear(readbuffer.Array, 0, readbuffer.Array.Length);

                    res = await sock.ReceiveAsync(readbuffer, CancellationToken.None);
                    actualData = readbuffer.Array.Take(res.Count).ToArray();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EXCEPTION {ex}");
                    if (!isDisconnectReasonByUser)
                    {
                        ErrorHappensReceived?.Invoke(this, HandleError(ex));
                    }
                    return;
                }

                switch (res.MessageType)
                {
                    case WebSocketMessageType.Binary:
                        if (!StaticUpdateMode)
                        {
                            byte[] payload = actualData;
                            payload = DecompressGzip(payload);
                            FrameReceived?.Invoke(this, ConvertToBitmapImage(payload).Result);
                        }
                        if (isServerLoadingComplete)
                        {
                            ServerSendComplete?.Invoke(this, true);
                        }
                        break;
                    case WebSocketMessageType.Close:
                        break;
                    case WebSocketMessageType.Text:
                        var jsonString = Encoding.UTF8.GetString(actualData);
                        try
                        {
                            var packet = JsonConvert.DeserializeObject<dynamic>(jsonString);
                            
                            if (packet.Type == "FullPageScreenshot")
                            {
                                string messageId = "FullPageScreenshot"; 
                                int chunkIndex = (int)packet.ChunkIndex;
                                int totalChunks = (int)packet.TotalChunks;
                                string chunkData = (string)packet.Data;

                                if (!_imageChunks.ContainsKey(messageId))
                                {
                                    _imageChunks[messageId] = new Dictionary<int, string>();
                                    _expectedChunks[messageId] = totalChunks;
                                }

                                if (chunkData.EndsWith("=="))
                                {
                                    chunkData = chunkData.Substring(0, chunkData.Length - 2);
                                }

                                _imageChunks[messageId][chunkIndex] = chunkData;

                                Debug.WriteLine($"Received chunk {chunkIndex + 1}/{totalChunks} for message {messageId}");

                                if (_imageChunks[messageId].Count == totalChunks)
                                {
                                    try
                                    {

                                        var fullImageData = string.Concat(_imageChunks[messageId].OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value));
                                        foreach (var kvp in _imageChunks[messageId].OrderBy(kvp => kvp.Key))
                                        {
                                            Debug.WriteLine($"Chunk {kvp.Key + 1}/{totalChunks}, length: {kvp.Value.Length}");
                                            Debug.WriteLine($"First 50 chars: {kvp.Value.Substring(0, Math.Min(50, kvp.Value.Length))}");
                                            Debug.WriteLine($"Last 50 chars: {kvp.Value.Substring(Math.Max(0, kvp.Value.Length - 50))}");
                                        }

                                        if (fullImageData.EndsWith("="))
                                        {
                                            fullImageData += "=";
                                        } 
                                        else
                                        {
                                            fullImageData += "==";
                                        }
                                        _imageChunks.Remove(messageId);
                                        _expectedChunks.Remove(messageId);

                                        var imageBytes = Convert.FromBase64String(fullImageData);
                                        var screenshot = await ConvertToBitmapImage(imageBytes);
                                        FullPageScreenshotReceived?.Invoke(this, screenshot);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Error processing full image data: {ex.Message}");
                                    }
                                }
                            }
                            else if (packet.Type == "TabScreenshot")
                            {
                                long tabId = (long)packet.TabId;
                                string base64Data = (string)packet.Data;

                                var imageBytes = Convert.FromBase64String(base64Data);
                                SaveTabScreenshot(tabId, imageBytes);
                                break;
                            }
                            else
                            {

                                TextPacketReceived?.Invoke(this, JsonConvert.DeserializeObject<TextPacket>(jsonString));
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing text message: {ex.Message}");
                            // ShowErrorDialogAsync("UNEXPECTED CRITICAL ERROR", $"Error processing text message: {ex.Message}");
                        }
                        break;
                    default:
                        break;
                }
            }
            if (!isDisconnectReasonByUser)
            {
                ErrorHappensReceived?.Invoke(this, $"ERR_NET_SYSTEM_DISCONNECT_STATUS_{sock.CloseStatus}");
            }
            Debug.WriteLine("Connection closed");
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView?.CoreWindow?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        var dialog = new Windows.UI.Xaml.Controls.ContentDialog
                        {
                            Title = title,
                            Content = message,
                            CloseButtonText = "OK"
                        };
                        await dialog.ShowAsync();
                    });
            }
            else
            {
                
            }
        }
        public async void RequestNewScreenshot(string size)
        {
            await CreateAndSendDefaultTextPacket(
                PacketType.NewScreenShotRequest, $"{size}"
                );
        }

        public async void CreateNewTabWithUrl(string targetUrl)
        {
            await CreateAndSendDefaultTextPacket(
                PacketType.OpenUrlInNewTab, targetUrl
                );
        }

        public async void RequestTabScreenshot(long tabId)
        {
            await CreateAndSendDefaultTextPacket(
                PacketType.RequestTabScreenshot, tabId.ToString()
                );
        }
        public async void SendGetTabsRequest()
        {
            if (sock.State != WebSocketState.Open)
                return;
            var encoded = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new CommPacket
            {
                PType = PacketType.GetTabsOpen
            }));
            var buffer = new ArraySegment<byte>(encoded, 0, encoded.Length);
            await sock.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async void CloseTab(long id)
        {
            await CreateAndSendDefaultTextPacket(
                PacketType.CloseTab, id.ToString()
                );
        }
        public async void SendModeChange(string mode)
        {
            await CreateAndSendDefaultTextPacket(
                PacketType.ModeChange, mode
                );
        }
        public async void SetActivePage(long id)
        {
            await CreateAndSendDefaultTextPacket(
                PacketType.SetActivePage, id.ToString()
                );
        }

        public async void Navigate(string s)
        {
            await CreateAndSendDefaultTextPacket(
                PacketType.Navigation, s
                );
        }

        public async void NavigateForward()
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.NavigateForward
            };

            await SendTextPacket(commPacket);
        }
        public async void NavigateBack()
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.NavigateBack
            };
            await SendTextPacket(commPacket);
        }
        public async void SendChar(string c)
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.SendChar,
                JSONData = c.ToString()
            };
            await SendTextPacket(commPacket);
        }

        public async void SendKeyCommand(string command)
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.SendKeyCommand,
                JSONData = command
            };
            await SendTextPacket(commPacket);
        }
        public async void SendKey(Windows.UI.Xaml.Input.KeyRoutedEventArgs key)
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.SendKey,
                JSONData = JsonConvert.SerializeObject(key.Key)
            };
            await SendTextPacket(commPacket);
        }

        public async void SizeChange(Windows.Foundation.Size newSize)
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.SizeChange,
                JSONData = JsonConvert.SerializeObject(newSize)
            };

            await SendTextPacket(commPacket);
        }

        public async void TouchDown(Point p, uint pointerId)
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.TouchDown,
                JSONData = JsonConvert.SerializeObject(new PointerPacket
                {
                    px = p.X,
                    py = p.Y,
                    id = pointerId
                })
            };

            await SendTextPacket(commPacket);


        }
        public async void TouchUp(Point p, uint pointerId)
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.TouchUp,
                JSONData = JsonConvert.SerializeObject(new PointerPacket
                {
                    px = p.X,
                    py = p.Y,
                    id = pointerId
                })
            };

            await SendTextPacket(commPacket);
        }


        public async void SendText(string text)
        {
            await CreateAndSendDefaultTextPacket(PacketType.TextInputSend, text);
        }
        public async void TouchMove(Point p, uint pointerId)
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.TouchMoved,
                JSONData = JsonConvert.SerializeObject(new PointerPacket
                {
                    px = p.X,
                    py = p.Y,
                    id = pointerId
                })
            };
            await SendTextPacket(commPacket);
        }

        public async void ACKRender()
        {
            CommPacket commPacket = new CommPacket
            {
                PType = PacketType.ACK
            };

            await SendTextPacket(commPacket);
        }

        private async Task CreateAndSendDefaultTextPacket(PacketType packetType, string dataToSend)
        {
            CommPacket commPacket = new CommPacket
            {
                PType = packetType,
                JSONData = dataToSend
            };

            await SendTextPacket(commPacket);
        }

        private async Task SendTextPacket(CommPacket commPacket)
        {
            if (sock.State != WebSocketState.Open)
                return;

            string PacketJSON = JsonConvert.SerializeObject(commPacket);
            var encoded = Encoding.UTF8.GetBytes(PacketJSON);
            var buffer = new ArraySegment<byte>(encoded, 0, encoded.Length);
            await sock.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private byte[] DecompressGzip(byte[] data)
        {
            using (var inMs = new MemoryStream(data))
            using (var gzip = new GZipStream(inMs, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                gzip.CopyTo(outMs);
                return outMs.ToArray();
            }
        }

        public async Task<BitmapImage> ConvertToBitmapImage(byte[] image)
        {
            if (image == null || image.Length == 0)
            {
                Debug.WriteLine("Image byte array is null or empty.");
                return null;
            }

            try
            {
                BitmapImage bitmapImage = new BitmapImage();
                using (InMemoryRandomAccessStream ms = new InMemoryRandomAccessStream())
                {
                    await ms.WriteAsync(image.AsBuffer());
                    ms.Seek(0);
                    bitmapImage.SetSource(ms);
                }
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting byte array to BitmapImage: {ex.Message}");
                return null;
            }
        }
        private async void SaveTabScreenshot(long tabId, byte[] imageBytes)
        {
            try
            {
                var folder = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    "TabsScreenshots", CreationCollisionOption.OpenIfExists
                    );
                var file = await folder.CreateFileAsync($"{tabId}.png", CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteBytesAsync(file, imageBytes);
                Debug.WriteLine($"Screenshot for tab {tabId} saved to {file.Path}");
                TabImageSendComplete?.Invoke(this, tabId);  
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving screenshot for tab {tabId}: {ex.Message}");
            }
        }
        public void Dispose()
        {
            isDisconnectReasonByUser = true;
            if (sock != null)
            {
                sock.Dispose();
                sock = null;
            }
        }
    }
}
