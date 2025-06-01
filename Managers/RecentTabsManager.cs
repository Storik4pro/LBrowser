using LinesBrowser.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media.Imaging;
using static LinesBrowser.MainPage;

namespace LinesBrowser.Managers
{
    public class RecentTabInfo : INotifyPropertyChanged
    {
        public long Id { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public DateTime ClosedAt { get; set; }

        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set => SetField(ref _isPinned, value);
        }
        public DateTime LastVisited { get; set; }
        public Windows.UI.Xaml.Media.Imaging.BitmapImage Thumbnail { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
    public class RecentTabsManager
    {
        private static readonly SemaphoreSlim FileLock = new SemaphoreSlim(1, 1);
        private const string RecentTabsFileName = "recent_tabs.json";
        private const string ScreenshotsFolder = "TabsScreenshots";

        private static ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        public static async Task<List<RecentTabInfo>> LoadRecentTabsAsync()
        {
            await FileLock.WaitAsync();
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                try
                {
                    var file = await folder.GetFileAsync(RecentTabsFileName);
                    var json = await FileIO.ReadTextAsync(file);
                    return JsonConvert.DeserializeObject<List<RecentTabInfo>>(json) ?? new List<RecentTabInfo>();
                }
                catch
                {
                    return new List<RecentTabInfo>();
                }
            }
            finally
            {
                FileLock.Release();
            }
        }

        public static async Task SaveRecentTabsAsync(List<RecentTabInfo> tabs)
        {
            if (!(bool)localSettings.Values["UseRecentFeature"])
                return;
            await FileLock.WaitAsync();
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(RecentTabsFileName, CreationCollisionOption.ReplaceExisting);
                var json = JsonConvert.SerializeObject(tabs);
                await FileIO.WriteTextAsync(file, json);
            }
            finally
            {
                FileLock.Release();
            }
        }

        public static async Task AddRecentTabAsync(RecentTabInfo tab)
        {
            var tabs = await LoadRecentTabsAsync();
            tabs.RemoveAll(t => t.Id == tab.Id);
            tabs.Insert(0, tab);
            await SaveRecentTabsAsync(tabs);
        }

        public static async Task SetPinnedStateAsync(long id, bool isPinned)
        {
            var tabs = await LoadRecentTabsAsync();
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].Id == id)
                {
                    tabs[i].IsPinned = isPinned;
                }
            }
            await SaveRecentTabsAsync(tabs);
        }


        public static async Task RemoveRecentTabAsync(long id)
        {
            var tabs = await LoadRecentTabsAsync();
            tabs.RemoveAll(t => t.Id == id);
            await SaveRecentTabsAsync(tabs);

            try
            {
                var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(ScreenshotsFolder);
                var file = await folder.GetFileAsync($"{id}.png");
                await file.DeleteAsync();
            }
            catch { }
        }

        public static async Task CleanupOrphanScreenshotsAsync()
        {
            var tabs = await LoadRecentTabsAsync();
            var ids = new HashSet<string>(tabs.Select(t => t.Id.ToString()));

            try
            {
                var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(ScreenshotsFolder);
                var files = await folder.GetFilesAsync();
                foreach (var file in files)
                {
                    var name = file.Name;
                    if (name.EndsWith(".png"))
                    {
                        var id = name.Replace(".png", "");
                        if (!ids.Contains(id))
                            await file.DeleteAsync();
                    }
                }
            }
            catch { }
        }

        public static async Task<Tuple<string, string>> GetRecentBytes()
        {
            var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(ScreenshotsFolder);
            IReadOnlyList<StorageFile> fileList = await folder.GetFilesAsync();

            string fileSize = "0";
            ulong fileSizeInBytes = 0;
            string fileSizePostfix = "bytes";

            foreach (StorageFile file in fileList)
            {
                Windows.Storage.FileProperties.BasicProperties basicProperties =
                    await file.GetBasicPropertiesAsync();
                fileSizeInBytes += basicProperties.Size;
                Debug.WriteLine(basicProperties.Size);
            }

            ulong fileSizeInKB = fileSizeInBytes / 1024;

            ulong fileSizeInMB = fileSizeInKB / 1024;

            if (fileSizeInMB >= 1)
            {
                fileSize = fileSizeInMB.ToString();
                fileSizePostfix = "MB";
            }
            else if (fileSizeInKB >= 1)
            {
                fileSize = fileSizeInKB.ToString();
                fileSizePostfix = "KB";
            }
            else
            {
                fileSize = fileSizeInBytes.ToString();
            }

                return Tuple.Create(fileSize, fileSizePostfix);
        }

        public static async Task DeleteAllUnusedScreenshots()
        {
            StatusBarProgressIndicator indicator = null;
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                StatusBar statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundOpacity = 1.0;

                indicator = statusBar.ProgressIndicator;

                indicator.ProgressValue = 1; 
                indicator.Text = "Working ...";
                await indicator.ShowAsync();
            }

            List<long> openedTabsId = StateHelper.Instance.OpenedTabsId;

            if (openedTabsId == null)
                return;

            var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(ScreenshotsFolder);
            IReadOnlyList<StorageFile> fileList = await folder.GetFilesAsync();

            foreach (StorageFile file in fileList)
            {
                bool flag = false;
                
                for (int i = 0; i < openedTabsId.Count; i++)
                {
                    if (openedTabsId[i].ToString() == file.Name.Replace(".png", ""))
                    {
                        flag = true;
                    }
                }

                // Debug.WriteLine($"File {file.Name} isRemoved:{!flag}");

                if (flag)
                    continue;

                try
                {
                    await file.DeleteAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Cannot delete file. Error is {ex}");
                }
            }

            var dataFolder = ApplicationData.Current.LocalFolder;
            var dataFile = await dataFolder.CreateFileAsync(RecentTabsFileName, CreationCollisionOption.ReplaceExisting);
            var json = JsonConvert.SerializeObject("{}");
            await FileIO.WriteTextAsync(dataFile, json);

            if (indicator != null)
            {
                await indicator.HideAsync();
            }
        }
    }
}
