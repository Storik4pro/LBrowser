using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace LinesBrowser
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConnectProgressPage : Page
    {
        private string _serverAddress;
        private string _audioServerAddress;
        private State _state;
        private string _error;
        private bool _isConnecting = false;
        private static ResourceLoader resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
        private static ResourceLoader errorResourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView("ErrorCodes");

        private bool isCriticalErrorHappens = false;
        private bool isErrorHappens = false;
        private bool isServerConnected = false;
        private bool isAudioServerConnected = false;
        private string errorCode = "";

        public enum State
        {
            DisconnectedWithError,
            Basic
        }

        public ConnectProgressPage()
        {
            this.InitializeComponent();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Tuple<State, string, string, string> param)
            {
                _serverAddress = param.Item2;
                _audioServerAddress = param.Item3;
                _state = param.Item1;
                _error = param.Item4;
                if (_state == State.Basic)
                    StartConnection();
                else
                    ShowError(_error);

            }
        }

        private void StartConnection()
        {
            _isConnecting = true;
            PageTitleTextBlock.Text = resourceLoader.GetString("ConnectState1");
            StatusTitle.Text = resourceLoader.GetString("WorkInProgress");
            StatusTextBlock.Text = resourceLoader.GetString("WorkingMessage");
            ErrorCodeText.Text = "";
            ProgressRing.Visibility = Visibility.Visible;
            RetryButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Visible;
            ContinueAnywayButton.Visibility = Visibility.Collapsed;
            isCriticalErrorHappens = false;
            isErrorHappens = false;
            isServerConnected = false;
            isAudioServerConnected = false;
            errorCode = "";

            ConnectionHelper.Instance.OnErrorHappens += ConnectionHelper_OnErrorHappens;
            ConnectionHelper.Instance.OnConnectionFailure += ConnectionHelper_OnConnectionFailure;
            ConnectionHelper.Instance.OnCriticalConnectionFailure += ConnectionHelper_OnCriticalConnectionFailure;
            ConnectionHelper.Instance.OnAllServerConnected += ConnectionHelper_OnAllServerConnected;
            ConnectionHelper.Instance.OnServerConnectedSuccessful += ConnectionHelper_OnServerConnectedSuccessful;

            ConnectionHelper.Instance.Connect(_serverAddress, _audioServerAddress);
        }

        private void ConnectionHelper_OnAllServerConnected(object sender, EventArgs e)
        {
            ProgressRing.Visibility = Visibility.Collapsed;
            Frame.Navigate(typeof(MainPage));
        }

        private void ConnectionHelper_OnServerConnectedSuccessful(object sender, EventArgs e)
        {
            isServerConnected = true;
            if (isErrorHappens)
            {
                ShowInfo(errorCode);
            } 

        }


        private void ConnectionHelper_OnConnectionFailure(object sender, string _errorCode)
        {
            errorCode = _errorCode;
            isErrorHappens = true;
            if (isServerConnected)
                ShowInfo(errorCode);
        }

        private void ConnectionHelper_OnCriticalConnectionFailure(object sender, string _errorCode)
        {
            isCriticalErrorHappens = true;
            ShowError(_errorCode);
        }

        private void ConnectionHelper_OnErrorHappens(object sender, string error)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ShowError(error);
            }).AsTask().Wait();
        }

        private void ShowError(string errorCode)
        {
            string _errorCodeText = errorResourceLoader.GetString(errorCode);
            string _emptyText = resourceLoader.GetString("ErrorHelperTextBasic");
            string errorText = (_errorCodeText == string.Empty ? _emptyText : _errorCodeText) + 
                $"\n\n" + 
                resourceLoader.GetString("ErrorHelperMessage");

            _isConnecting = false;
            ProgressRing.Visibility = Visibility.Collapsed;
            PageTitleTextBlock.Text = resourceLoader.GetString("ConnectState2");
            StatusTitle.Text = resourceLoader.GetString("ErrorHappensTitle");
            StatusTextBlock.Text = errorText;
            ErrorCodeText.Text = errorCode;
            RetryButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            ContinueAnywayButton.Visibility = Visibility.Collapsed;
            ConnectionHelper.Instance.OnErrorHappens -= ConnectionHelper_OnErrorHappens;
        }

        private void ShowInfo(string errorCode)
        {
            string _errorCodeText = errorResourceLoader.GetString(errorCode);
            string _emptyText = resourceLoader.GetString("ErrorHelperTextBasic");
            string errorText = (_errorCodeText == string.Empty ? _emptyText : _errorCodeText) + 
                $"\n\n" +
                resourceLoader.GetString("ErrorHelperNotCriticalMessage"); ;
            _isConnecting = false;
            ProgressRing.Visibility = Visibility.Collapsed;
            PageTitleTextBlock.Text = resourceLoader.GetString("ConnectState3");
            StatusTitle.Text = resourceLoader.GetString("AudioServerNotConnected");
            StatusTextBlock.Text = errorText;
            ErrorCodeText.Text = errorCode;
            RetryButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            ContinueAnywayButton.Visibility = Visibility.Visible;
            ConnectionHelper.Instance.OnErrorHappens -= ConnectionHelper_OnErrorHappens;
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            StartConnection();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnecting)
            {
                ConnectionHelper.Instance.OnErrorHappens -= ConnectionHelper_OnErrorHappens;
                ConnectionHelper.Instance.Disconnect();
            }
            Frame.Navigate(typeof(ConnectPage));
        }

        private void ContinueAnywayButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }
    }
}
