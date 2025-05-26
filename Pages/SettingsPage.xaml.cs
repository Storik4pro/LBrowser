using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Globalization;
using Windows.ApplicationModel.Resources.Core;
using Windows.ApplicationModel.Resources;
using System.Diagnostics;
using Windows.Storage;
using System.Globalization;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace LinesBrowser
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private static ResourceLoader resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
        private static ApplicationDataContainer settings = Windows.Storage.ApplicationData.Current.LocalSettings;
        public class LanguageItem
        {
            public string Code { get; set; } 
            public string DisplayName { get; set; } 
        }
        public List<LanguageItem> Languages { get; } = new List<LanguageItem>
        {
            new LanguageItem { Code = "en-US", DisplayName = resourceLoader.GetString("EN/Text") },
            new LanguageItem { Code = "ru-RU", DisplayName = resourceLoader.GetString("RU/Text") },
        };
        public class ThanksItem
        {
            public string Name { get; set; }
            public string Link { get; set; }
        }
        public SettingsPage()
        {
            this.InitializeComponent();

            var version = Package.Current.Id.Version;
            VersionTextBlock.Text = $"{resourceLoader.GetString("Version/Text")}: {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            DeveloperTextBlock.Text = $"{resourceLoader.GetString("Developer/Text")}: Storik4";
            SystemInfoTextBlock.Text = $"{resourceLoader.GetString("SystemInfo/Text")}: {GetSystemInfo()}";

            var thanksList = new List<ThanksItem>
            {
                new ThanksItem { Name = "PreyK", Link = "https://github.com/PreyK" },
            };
            FillThanksPanel(thanksList);


            if (Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Desktop")
            {
                var navigationManager = SystemNavigationManager.GetForCurrentView();
                navigationManager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            }
            SystemNavigationManager.GetForCurrentView().BackRequested += System_BackRequested;
            AutoConnectCheckBox.IsChecked = settings.Values["AutoConnect"] as bool?;

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

                WikiUrl.NavigateUri = new Uri($"https://storik4pro.github.io/{langTag}/LBrowser/wiki");

            // LagTextBox.Text = (settings.Values["preferredLag"] as string)?? "2";
        }
        private string GetSystemInfo()
        {
            var deviceFamily = AnalyticsInfo.VersionInfo.DeviceFamily;
            var arch = Package.Current.Id.Architecture.ToString();
            return $"{deviceFamily}, {arch}";
        }

        private void FillThanksPanel(List<ThanksItem> thanksList)
        {
            ThanksPanel.Children.Clear();
            foreach (var item in thanksList)
            {
                if (!string.IsNullOrEmpty(item.Link) && item.Link != "none")
                {
                    var link = new HyperlinkButton
                    {
                        Content = $"{item.Name} — {item.Link}",
                        NavigateUri = new Uri(item.Link),
                    };
                    ThanksPanel.Children.Add(link);
                }
                else
                {
                    var text = new TextBlock
                    {
                        Text = item.Name,
                    };
                    ThanksPanel.Children.Add(text);
                }
            }
        }
        private void System_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Desktop")
            {
                var navigationManager = SystemNavigationManager.GetForCurrentView();
                navigationManager.BackRequested -= System_BackRequested;
                navigationManager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            }
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is LanguageItem selected)
            {
                settings.Values["AppLanguage"] = selected.Code;

                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = selected.Code;
            }
        }

        private void LanguageComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            string currentLang = settings.Values["AppLanguage"] as string;
            if (string.IsNullOrEmpty(currentLang))
                currentLang = Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault() ?? "en-US";
            LanguageComboBox.SelectedValue = currentLang;
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectionHelper.Instance.Disconnect();
            Frame.Navigate(typeof(ConnectPage));
        }

        private void AutoConnectCheckBox_Click(object sender, RoutedEventArgs e)
        {
            settings.Values["AutoConnect"] = AutoConnectCheckBox.IsChecked;
        }
    }
}
