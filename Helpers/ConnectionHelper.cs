using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media.Imaging;

namespace LinesBrowser
{
    internal class ConnectionHelper
    {
        private static ConnectionHelper _instance;
        private static readonly object _lock = new object();
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

        private bool isConnectionToServerComplete;
        private bool isConnectionToAudioServerComplete;
        private bool isConnectionToAudioServerEnabled;

        public static ConnectionHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ConnectionHelper();
                    return _instance;
                }
            }
        }

        private string _serverAddress;
        public string ServerAddress
        {
            get { return _serverAddress; }
            set
            {
                if (_serverAddress != value)
                {
                    _serverAddress = value;
                    
                }
            }
        }
        private string _audioServerAddress;
        public string AudioServerAddress
        {
            get { return _audioServerAddress; }
            set
            {
                if (_audioServerAddress != value)
                {
                    _audioServerAddress = value;

                }
            }
        }

        public EventHandler<string> OnErrorHappens;
        public EventHandler OnServerConnectedSuccessful;
        public EventHandler OnAudioServerConnectedSuccessful;
        public EventHandler OnAllServerConnected;
        public EventHandler<string> OnCriticalConnectionFailure;
        public EventHandler<string> OnConnectionFailure;
        public EventHandler<string> OnDisconnected;

        public WebBrowserDataSource webBrowserDataSource;
        public Network.AudioStreamerClient audioStreamerClient;

        private ConnectionHelper()
        {
            isConnectionToServerComplete = false;
            isConnectionToAudioServerComplete = false;
            isConnectionToAudioServerEnabled = true;
        }

        public bool Connect(string serverAddress, string audioServerAddress)
        {
            isConnectionToServerComplete = false;
            isConnectionToAudioServerComplete = false;
            isConnectionToAudioServerEnabled = audioServerAddress != null;

            ServerAddress = serverAddress;

            if (serverAddress == null || !serverAddress.Contains("ws://") || serverAddress.Split(':').Length < 2)
            {
                return false;
            }

            settings.Values["LastServerUrl"] = ServerAddress;

            try
            {
                webBrowserDataSource?.Dispose();
                webBrowserDataSource = new WebBrowserDataSource();
                webBrowserDataSource.ServerConnectComplete += OnServerConnected;
                webBrowserDataSource.ErrorHappensReceived += WebBrowserDataSource_ErrorHappensReceived;
                webBrowserDataSource.StartReceive(serverAddress);

                if (audioServerAddress != null)
                {
                    audioStreamerClient?.Dispose();
                    audioStreamerClient = new Network.AudioStreamerClient();
                    audioStreamerClient.ServerConnected += OnAudioServerConnected;
                    _ = audioStreamerClient.StartAsync(audioServerAddress);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AUDIO Stream error {ex}");
                OnErrorHappens?.Invoke(this, ex.Message);
                return false;
            }
            return true;
        }

        private void WebBrowserDataSource_ErrorHappensReceived(object sender, string e)
        {
            Disconnect();
            OnDisconnected?.Invoke(this, e);
        }

        private void OnServerConnected(object sender, Tuple<bool, string> args)
        {
            bool state = args.Item1;
            string errorCode = args.Item2;
            isConnectionToServerComplete = state;
            if (state)
            {
                OnServerConnectedSuccessful?.Invoke(this, EventArgs.Empty);

                if (!isConnectionToAudioServerEnabled || isConnectionToAudioServerComplete)
                {
                    OnAllServerConnected?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                OnCriticalConnectionFailure?.Invoke(this, errorCode);
                Disconnect();
            }
            
        }

        private void OnAudioServerConnected(object sender, Tuple<bool, string> args)
        {
            bool state = args.Item1;
            string errorCode = args.Item2;
            isConnectionToAudioServerComplete = state;
            if (state)
            {
                OnAudioServerConnectedSuccessful?.Invoke(this, EventArgs.Empty);

                if (isConnectionToServerComplete)
                {
                    OnAllServerConnected?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                OnConnectionFailure?.Invoke(this, errorCode);
            }
        }

        public void Disconnect()
        {
            webBrowserDataSource?.Dispose();
            audioStreamerClient?.Dispose();
            webBrowserDataSource = null;
            audioStreamerClient = null;
        }

    }
}
