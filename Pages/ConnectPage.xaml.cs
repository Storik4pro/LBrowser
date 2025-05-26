using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
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
    public sealed partial class ConnectPage : Page
    {
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private static ResourceLoader resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
        public ConnectPage()
        {
            this.InitializeComponent();
            ServerAddressTextBox.Text = settings.Values["LastServerUrl"] as string ?? "ws://server:8081";
            AudioServerAddressTextBox.Text = settings.Values["AudioServerAddress"] as string ?? "";
            EnableAudioStream.IsChecked = settings.Values["EnableAudioStream"] as bool? ?? true;
            AutoConnectCheckBox.IsChecked = settings.Values["AutoConnect"] as bool? ?? true;

            EnableAudioStream.Content += $" ({resourceLoader.GetString("BetaTestString")})";
            ShowAdditionalSettingsButtonText.Text = resourceLoader.GetString("ShowAdditionalSettings");

            string _langTag = CultureInfo.CurrentCulture.Name;
            string langTag;

            if (_langTag == "ru")
            {
                langTag = "ru-RU";
            }
            else
            {
                langTag = "en-US";
            }

            WikiQUrl.NavigateUri = new Uri($"https://storik4pro.github.io/{langTag}/LBrowser/wiki/what-i-need-to-do-for-start/");
            WikiUrl.NavigateUri = new Uri($"https://storik4pro.github.io/{langTag}/LBrowser/");

        }

        private void ShowAdditionalSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdditionalSettingsStackPanel.Visibility == Visibility.Visible)
            {
                AdditionalSettingsStackPanel.Visibility = Visibility.Collapsed;
                ShowAdditionalSettingsButtonText.Text = resourceLoader.GetString("ShowAdditionalSettings");
                ChevronIcon.Glyph = "\uE70D"; 
            }
            else
            {
                AdditionalSettingsStackPanel.Visibility = Visibility.Visible;
                ShowAdditionalSettingsButtonText.Text = resourceLoader.GetString("HideAdditionalSettings");
                ChevronIcon.Glyph = "\uE70E"; 
            }
        }

        private void ServerAddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool isAudioServerAutocomplete = settings.Values["AutocompleteAudioServer"] as bool? ?? true;
            if (ServerAddressTextBox.Text.Split(':').Length == 3 && isAudioServerAutocomplete)
                AudioServerAddressTextBox.Text = "ws:" + ServerAddressTextBox.Text.Split(':')[1] + ":8082";
            ErrGrid.Visibility = Visibility.Collapsed;
        }

        private void ResetAudioServerSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            settings.Values["AutocompleteAudioServer"] = true;
            if (ServerAddressTextBox.Text.Split(':').Length == 3)
                AudioServerAddressTextBox.Text = "ws:" + ServerAddressTextBox.Text.Split(':')[1] + ":8082";
            else
                AudioServerAddressTextBox.Text = "";
        }

        private void AudioServerAddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (FocusManager.GetFocusedElement() == sender)
                settings.Values["AutocompleteAudioServer"] = false;
            ErrGrid.Visibility = Visibility.Collapsed;
        }

        private void EnableAudioStream_Click(object sender, RoutedEventArgs e)
        {

        }

        private bool IsValidAddress(string address)
        {
            address = address.Replace(" ", string.Empty);
            if (address.Split(':').Length == 3 && (address.StartsWith("tcp://") || address.StartsWith("ws://")))
                return true;
            return false;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string serverAddress = ServerAddressTextBox.Text;
            string audioServerAddress = AudioServerAddressTextBox.Text;
            bool enableAudioStream = (bool)EnableAudioStream.IsChecked;

            if (!IsValidAddress(serverAddress))
            {
                ErrGrid.Visibility = Visibility.Visible;
                ErrGridText.Text = resourceLoader.GetString("ServerAddressInvalid");
                return;
            }

            if (audioServerAddress.Replace(" ", string.Empty) == string.Empty)
            {
                audioServerAddress = "ws:" + serverAddress.Split(':')[1] + ":8082";
            }

            if (enableAudioStream && !IsValidAddress(audioServerAddress))
            {
                ErrGrid.Visibility = Visibility.Visible;
                ErrGridText.Text = resourceLoader.GetString("AudioServerAddressInvalid");
                return;
            } 
            else if (!enableAudioStream)
            {
                audioServerAddress = null;
            }

            settings.Values["serverAddress"] = serverAddress;
            settings.Values["AutoConnect"] = AutoConnectCheckBox.IsChecked;
            settings.Values["EnableAudioStream"] = enableAudioStream;

            if (audioServerAddress != null)
                settings.Values["audioServerAddress"] = audioServerAddress;

            Frame.Navigate(typeof(ConnectProgressPage), Tuple.Create(ConnectProgressPage.State.Basic, serverAddress, audioServerAddress, ""));
        }
    }
}
