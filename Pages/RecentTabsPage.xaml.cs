using LinesBrowser.Managers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Search.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace LinesBrowser.Pages
{
    public sealed partial class RecentTabsPage : Page
    {
        private List<RecentTabInfo> _allRecentTabs = new List<RecentTabInfo>();
        private ObservableCollection<RecentTabInfo> _filteredTabs = new ObservableCollection<RecentTabInfo>();

        public RecentTabsPage()
        {
            this.InitializeComponent();
            RecentTabsListView.ItemsSource = _filteredTabs;

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1, 0))
            {
                Windows.Phone.UI.Input.HardwareButtons.BackPressed += OnHardwareBackPressed;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadRecentTabsAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1, 0))
            {
                Windows.Phone.UI.Input.HardwareButtons.BackPressed -= OnHardwareBackPressed;
            }
        }

        private void OnHardwareBackPressed(object sender, BackPressedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
            else
            {
                e.Handled = false;
            }
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
            else
            {
                e.Handled = false;
            }
        }

        private void AuditErrorVisibility()
        {
            NothingErrStackPanel.Visibility = _filteredTabs.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async Task LoadRecentTabsAsync()
        {
            var allRecent = await RecentTabsManager.LoadRecentTabsAsync();
            var openTabIds = ((App.Current as App)?.MainPageInstance?.GetOpenTabIds() ?? new List<long>());
            _allRecentTabs = allRecent.Where(t => !openTabIds.Contains(t.Id)).ToList();

            foreach (var tab in _allRecentTabs)
            {
                tab.Thumbnail = await GetScreenshotForTab(tab.Id);
            }


            ApplyFilter(SearchSuggestBox.Text);

            AuditErrorVisibility();
        }

        private void ApplyFilter(string query)
        {
            _filteredTabs.Clear();

            var sorted = _allRecentTabs
                .OrderByDescending(t => t.IsPinned)
                .ThenByDescending(t => t.LastVisited) 
                .ToList();

            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (var tab in sorted)
                    _filteredTabs.Add(tab);
            }
            else
            {
                query = query.Trim();
                foreach (var tab in sorted)
                {
                    if (tab.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tab.Url.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _filteredTabs.Add(tab);
                    }
                }
            }
            AuditErrorVisibility();
        }

        private void SearchSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            ApplyFilter(sender.Text);
        }


        private async Task<BitmapImage> GetScreenshotForTab(long tabId)
        {
            try
            {
                var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync("TabsScreenshots");
                var file = await folder.GetFileAsync($"{tabId}.png");
                using (var stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    var bitmap = new BitmapImage();
                    bitmap.SetSource(stream);
                    return bitmap;
                }
            }
            catch
            {
                return new BitmapImage(new System.Uri("ms-appx:///Assets/placeholder.png"));
            }
        }

        private async void DeleteRecentTab_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Debug.WriteLine($"DEL");
            var button = sender as Button;
            var tab = button?.DataContext as RecentTabInfo;
            Debug.WriteLine($"DEL {tab}");
            if (tab != null)
            {
                await RecentTabsManager.RemoveRecentTabAsync(tab.Id);
                await LoadRecentTabsAsync();
            }
        }

        private async void PinRecentTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RecentTabInfo tab)
            {
                tab.IsPinned = !tab.IsPinned;

                await RecentTabsManager.SetPinnedStateAsync(tab.Id, tab.IsPinned);
                
                ApplyFilter(SearchSuggestBox.Text);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage), "/tabs");
        }

        private void CloseTabsPageButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void RecentTabsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var tab = e.ClickedItem as RecentTabInfo;
            if (tab != null)
            {
                (App.Current as App)?.MainPageInstance?.OpenTabFromRecent(tab);
                Frame.GoBack();
            }
        }
    }
}
