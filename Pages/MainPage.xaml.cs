using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml.Controls;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.Graphics.Display;
using Windows.Foundation;
using Windows.UI.Input;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using LinesBrowser;
using Newtonsoft.Json;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.UI.Xaml.Media;
using Windows.UI.Core;
using Windows.System.Display;
using LinesBrowser.Managers;
using LinesBrowser.Helpers;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml.Navigation;
using Windows.Foundation.Metadata;
using Windows.Phone.UI.Input;

namespace LinesBrowser
{
    /// <summary>
    /// That code needs refactoring. Now it is impossible to read and modify it.
    /// But, I don't have enough time for that right now
    /// </summary>
    public sealed partial class MainPage : Page
    {
        WebBrowserDataSource webBrowserDataSource = ConnectionHelper.Instance.webBrowserDataSource;
        Network.AudioStreamerClient audioStreamerClient = ConnectionHelper.Instance.audioStreamerClient;
        public UdpClient sendingClient;
        public UdpClient recivingClient;

        public string broadcastAddress = "255.255.255.255";
        Timer UdpDiscoveryTimer;

        private long activeTabId = 0;

        private Dictionary<long, Task> screenshotUpdateQueue = new Dictionary<long, Task>();
        private Dictionary<long, TaskCompletionSource<bool>> screenshotCompletionSources = new Dictionary<long, TaskCompletionSource<bool>>();

        private string defaultNewPageUrl = "https://google.com/";

        private bool isCanGoBack = false;
        private bool isCanGoForward = false;

        private static ResourceLoader resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
        ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        public MainPage()
        {
            this.InitializeComponent();
            if (IsMobile)
            {
                Windows.UI.ViewManagement.StatusBar statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                _ = statusBar?.HideAsync();
            }
            
            ApplicationView.GetForCurrentView().VisibleBoundsChanged += OnVisibleBoundsChanged;

            if (localSettings.Values.ContainsKey("LastServerUrl"))
            {
                Debug.WriteLine("Has key");
                Debug.WriteLine(localSettings.Values["LastServerUrl"] as string);
                serverAddress.Text = localSettings.Values["LastServerUrl"] as string;
            }
            else
            {
                Debug.WriteLine("No known server");
            }
            var inputPane = InputPane.GetForCurrentView();
            inputPane.Showing += InputPane_Showing;
            inputPane.Hiding += InputPane_Hiding;
            EntryNavBar.Width = Window.Current.Bounds.Width;
            Window.Current.SizeChanged += Current_SizeChanged;
            Canvas.SetTop(EntryNavBar, Window.Current.Bounds.Height - EntryNavBar.Height);
            Canvas.SetTop(TextInput, Window.Current.Bounds.Height - EntryNavBar.Height);
            TextInput.Width = EntryNavBar.Width;

            MoreSettingsGrid.Width = Window.Current.Bounds.Width;
            OverlaySettingsLinksGrid.Width = MoreSettingsGrid.Width;
            NavbarGrid.Width = MoreSettingsGrid.Width;
            CertGrid.Width = MoreSettingsGrid.Width;
            CertGrid.Height = Window.Current.Bounds.Height;
            Canvas.SetTop(MoreSettingsGrid, Window.Current.Bounds.Height - EntryNavBar.Height - MoreSettingsGrid.Height);
            MoreSettingsAppBar.Loaded += MoreSettingsAppBar_Loaded;

            Canvas.SetTop(NavbarGrid, Window.Current.Bounds.Height - NavbarGrid.Height);
            SetupTabWidth();
            ConnectHandlers();

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1, 0))
            {
                Windows.Phone.UI.Input.HardwareButtons.BackPressed += OnBackButtonPressed;
            }

            DisplayRequest displayRequest = new DisplayRequest();
            displayRequest.RequestActive();

            ConnectionHelper.Instance.webBrowserDataSource.RequestCert();

        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string routeParameter)
            {
                if (routeParameter == "/tabs")
                {
                    OpenTabsPage();
                }
            }
        }

        public bool GoAppBack()
        {
            if (CertGrid.Visibility == Visibility.Visible)
            {
                CertGrid.Visibility = Visibility.Collapsed;
                return true;
            }
            if (isCanGoBack)
            {
                webBrowserDataSource.NavigateBack();
                TogglePageLoadingMode(true);
                return true;
            }
            return false;
        }

        private void OnBackButtonPressed(object sender, BackPressedEventArgs e)
        {
            GoAppBack();
            e.Handled = true;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            GoAppBack();
            e.Handled = true;
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, UpdateNavbarPosition);
        }

        private void UpdateNavbarPosition()
        {
            var visible = ApplicationView.GetForCurrentView().VisibleBounds;

            double navHeight = NavbarGrid.ActualHeight;

            double top = visible.Height - navHeight;

            Canvas.SetTop(NavbarGrid, top);

            test.Height = visible.Height - NavbarGrid.Height;
            test.Width = visible.Width;

            CertGrid.Width = visible.Width;
            CertGrid.Height = visible.Height;
        }

        const double MINTABWIDTH = 150;
        private double DefaultTabWidth = MINTABWIDTH;
        private void SetupTabWidth()
        {
            double _tabGridContentWidth = (Window.Current.Bounds.Width - 20);
            double row = Math.Floor(_tabGridContentWidth / MINTABWIDTH);
            double newWidth = _tabGridContentWidth / row - 4.5;
            DefaultTabWidth = newWidth;
            foreach (var item in TabsList.Items)
            {
                var container = TabsList.ContainerFromItem(item) as GridViewItem;
                if (container != null)
                {
                    var border = FindChild<Border>(container);
                    Debug.WriteLine($"{border.Width}, {newWidth}");
                    if (border != null)
                    {
                        border.Width = newWidth;
                        border.Height = newWidth;
                    }
                }
            }
        }
        private T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T foundChild)
                {
                    return foundChild;
                }
                var result = FindChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
        private void MoreSettingsAppBar_Loaded(object sender, RoutedEventArgs e)
        {
            var visible = ApplicationView.GetForCurrentView().VisibleBounds;

            MoreSettingsGrid.Height = MoreSettingsAppBar.ActualHeight;
            OverlaySettingsLinksGrid.Height = SettingsLinksScrollView.ActualHeight;

            Canvas.SetTop(MoreSettingsGrid, visible.Height - EntryNavBar.Height - MoreSettingsGrid.Height);
            Canvas.SetTop(OverlaySettingsLinksGrid, visible.Height - EntryNavBar.Height - MoreSettingsGrid.Height -
                OverlaySettingsLinksGrid.Height);
            Canvas.SetTop(NavbarGrid, visible.Height - EntryNavBar.Height);

            OverlayMoreSettingsCanvas.Visibility = Visibility.Collapsed;
            OverlaySettingsLinks.Visibility = Visibility.Collapsed;
            SettingsLinksGridHide.To = SettingsLinksScrollView.ActualHeight+50;
        }
        private void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            var visible = ApplicationView.GetForCurrentView().VisibleBounds;

            Canvas.SetTop(EntryNavBar, visible.Height - EntryNavBar.Height);
            EntryNavBar.Width = visible.Width;
            Canvas.SetTop(TextInput, visible.Height - EntryNavBar.Height);
            TextInput.Width = visible.Width;
            MoreSettingsGrid.Width = visible.Width;
            NavbarGrid.Width = visible.Width;
            ScreenshotImage.Width = visible.Width;
            OverlaySettingsLinksGrid.Width = MoreSettingsGrid.Width;

            CertGrid.Width = visible.Width;
            CertGrid.Height = visible.Height;

            OverlayFocusRectangle.Height = visible.Height;
            OverlayFocusRectangle.Width = visible.Width;

            TabsList.Width = visible.Width;

            Canvas.SetTop(NavbarGrid, visible.Height - NavbarGrid.Height);

            SetupTabWidth();
        }
        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            var visible = ApplicationView.GetForCurrentView().VisibleBounds;
            Canvas.SetTop(
                EntryNavBar, 
                Window.Current.Bounds.Height - (Window.Current.Bounds.Height - visible.Height) - args.OccludedRect.Height - EntryNavBar.ActualHeight
                );
            EntryNavBar.Width = Window.Current.Bounds.Width; 
            args.EnsuredFocusedElementInView = true; 
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            EntryNavBar.Width = Window.Current.Bounds.Width;
            Canvas.SetTop(EntryNavBar, Window.Current.Bounds.Height - 50);
        }
        private void LoseFocus(object sender)
        {
            var control = sender as Control;
            var isTabStop = control.IsTabStop;
            control.IsTabStop = false;
            control.IsEnabled = false;
            control.IsEnabled = true;
            control.IsTabStop = isTabStop;
        }
        private void TextBox_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var url = urlField.Text;
                urlViewField.Text = url;
                webBrowserDataSource.Navigate(url);
                TogglePageLoadingMode(true);
                e.Handled = true; LoseFocus(sender);
            }
        }
        public static bool IsMobile
        {
            get
            {
                var qualifiers = Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().QualifierValues;
                return (qualifiers.ContainsKey("DeviceFamily") && qualifiers["DeviceFamily"] == "Mobile");
            }
        }

        private void Page_SizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
           
        }

        private void Test_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var x = e.GetCurrentPoint(null).Position.X / ScaleRect.ActualWidth;
            var y = e.GetCurrentPoint(null).Position.Y / ScaleRect.ActualHeight;
            webBrowserDataSource?.TouchDown(new Point(x, y), e.Pointer.PointerId);

            NavbarGrid.Visibility = Visibility.Visible;
            TextInput.Visibility = Visibility.Collapsed;
        }

        private void Test_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var x = e.GetCurrentPoint(null).Position.X / ScaleRect.ActualWidth;
            var y = e.GetCurrentPoint(null).Position.Y / ScaleRect.ActualHeight;
            webBrowserDataSource?.TouchUp(new Point(x, y), e.Pointer.PointerId);
        }

        private void Test_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var x = e.GetCurrentPoint(null).Position.X / ScaleRect.ActualWidth;
            var y = e.GetCurrentPoint(null).Position.Y / ScaleRect.ActualHeight;
            webBrowserDataSource?.TouchMove(new Point(x, y), e.Pointer.PointerId);
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Connect(serverAddress.Text.Replace("tcp://", "ws://"), audioAddress.Text.Replace("tcp://", "ws://"));
            ConnectPage.Visibility = Visibility.Collapsed;
        }

        private void ConnectHandlers()
        {
            if (webBrowserDataSource == null)
            {
                return;
            }
            webBrowserDataSource.FrameReceived += (sender, bitmap) =>
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    test.Source = bitmap;
                    test.Visibility = Visibility.Visible;
                    ScreenshotScrollViewer.Visibility = Visibility.Collapsed;
                });
            };

            webBrowserDataSource.FullPageScreenshotReceived += (sender, screenshot) =>
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ScreenshotImage.Source = screenshot;
                    ScreenshotScrollViewer.Visibility = Visibility.Visible;
                    test.Visibility = Visibility.Collapsed;
                });
            };
            webBrowserDataSource.ServerSendComplete += (sender, state) =>
            {
                TogglePageLoadingMode(false);
            };
            webBrowserDataSource.TextPacketReceived += (s, o) =>
            {
                TextPacket textPacket = JsonConvert.DeserializeObject<TextPacket>(o);
                switch (textPacket.PType)
                {
                    case TextPacketType.NavigatedUrl:
                        urlField.Text = textPacket.text;
                        urlViewField.Text = textPacket.text;
                        webBrowserDataSource.SizeChange(new Size { Width = ScaleRect.ActualWidth, Height = ScaleRect.ActualHeight });
                        break;

                    case TextPacketType.OpenPages:
                        _ = UpdateScreenshotAsync(activeTabId);
                        var pages = textPacket.text.Split(';').ToList();
                        var lastPage = pages.Last().Split('|').ToList()[1];
                        UpdateTabsGrid(pages);
                        long _lastPage = 000000;

                        Int64.TryParse(lastPage, out _lastPage);
                        webBrowserDataSource.SetActivePage(_lastPage);
                        activeTabId = _lastPage;
                        break;

                    case TextPacketType.EditOpenTabTitle:
                        var splitUrl = textPacket.text.Split('|').ToList();
                        if (splitUrl.Count >= 2)
                        {
                            Int64.TryParse(splitUrl[1], out long id);
                            var title = splitUrl[0];

                            var tabToUpdate = tabs.FirstOrDefault(tab => tab.Id == id);
                            if (tabToUpdate != null)
                            {
                                tabToUpdate.Title = title;
                            }

                        }
                        break;

                    case TextPacketType.TextInputContent:
                        NavbarGrid.Visibility = Visibility.Collapsed;
                        TextInput.Visibility = Visibility.Visible;
                        Debug.WriteLine($"TEXT > {textPacket.text}");
                        websiteTextBox.Text = textPacket.text?? "";
                        websiteTextBox.Select(websiteTextBox.Text.Length, 0);
                        websiteTextBox.Focus(FocusState.Programmatic);
                        break;

                    case TextPacketType.TextInputSend:
                        break;

                    case TextPacketType.TextInputCancel:
                        TextInput.Visibility = Visibility.Collapsed;
                        NavbarGrid.Visibility = Visibility.Visible;
                        websiteTextBox.Text = "";
                        break;
                    case TextPacketType.LoadingStateChanged:
                        if (textPacket.text == "LOADING")
                        {
                            TogglePageLoadingMode(true);
                            webBrowserDataSource.isServerLoadingComplete = false;
                        }
                        else
                        {
                            webBrowserDataSource.isServerLoadingComplete = true;
                        }
                        break;
                    case TextPacketType.IsClientCanSendGoBackRequest:
                        if (textPacket.text == "true")
                        {
                            isCanGoBack = true;
                            BackButton.IsEnabled = isCanGoBack;
                        }
                        else
                        {
                            isCanGoBack = false;
                            BackButton.IsEnabled = isCanGoBack;
                        }
                        break;
                    case TextPacketType.IsClientCanSendGoForwardRequest:
                        if (textPacket.text == "true")
                        {
                            isCanGoForward = true;
                            ForwardButton.IsEnabled = isCanGoForward;
                        }
                        else
                        {
                            isCanGoForward = false;
                            ForwardButton.IsEnabled = isCanGoForward;
                        }
                        break;
                    case TextPacketType.ConnectionState:
                        ConnectionSecurePacket connectionSecurePacket = JsonConvert.DeserializeObject<ConnectionSecurePacket>(o);
                        Debug.WriteLine(connectionSecurePacket.Issuer);

                        TogglePageLoadingMode(false);
                        webBrowserDataSource.isServerLoadingComplete = true;
                        PopulateInfoGrid(connectionSecurePacket);
                        break;
                }
            };
            webBrowserDataSource.TabImageSendComplete += (s, o) =>
            {
                if (screenshotCompletionSources.ContainsKey(o))
                {
                    screenshotCompletionSources[o].TrySetResult(true);
                }
            };
            ConnectPage.Visibility = Visibility.Collapsed;
            var visible = ApplicationView.GetForCurrentView().VisibleBounds;
            string size = $"{visible.Width}x{visible.Height-NavbarGrid.Height}";
            webBrowserDataSource.RequestNewScreenshot(size);
        }

        public void Connect(string endpoint, string audioEndpoint)
        {
            ConnectionHelper.Instance.Connect(endpoint, audioEndpoint);

            webBrowserDataSource = ConnectionHelper.Instance.webBrowserDataSource;
            ConnectHandlers();

            webBrowserDataSource.SendGetTabsRequest();

            audioStreamerClient = ConnectionHelper.Instance.audioStreamerClient;
        }
        private void WebsiteTextBox_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // webBrowserDataSource.SendKey(e);

            string inputChar = null;
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                webBrowserDataSource.SendKeyCommand("Enter");
                return;
            }
            else if (e.Key == Windows.System.VirtualKey.Back)
            {
                webBrowserDataSource.SendKeyCommand("Backspace");
                return;
            } 
            else if (e.Key != Windows.System.VirtualKey.Shift && e.Key != Windows.System.VirtualKey.Control)
            {
                var tb = sender as TextBox;
                Debug.WriteLine($"tb is {tb}, {tb.Text.Length}");
                if (tb != null && tb.Text.Length > 0)
                {
                    inputChar = tb.Text.Last().ToString();
                }
            }
            Debug.WriteLine($"e.Key {e.Key}");
            if (!string.IsNullOrEmpty(inputChar))
            {
                var shift = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
                var ctrl = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var alt = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);

                var inputLanguage = Windows.Globalization.Language.CurrentInputMethodLanguageTag;

                var packet = new KeyCharPacket
                {
                    JSONData = inputChar,
                    Shift = shift,
                    Ctrl = ctrl,
                    Alt = alt,
                    Layout = inputLanguage
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(packet);
                Debug.WriteLine($"JSON {json.ToString()}");
                webBrowserDataSource.SendChar(json);
            }
        }
        private void WebsiteTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            
        }
        private void SendText_Click(object sender, RoutedEventArgs e)
        {
            webBrowserDataSource.SendText(websiteTextBox.Text);
            TextInput.Visibility = Visibility.Collapsed;
            NavbarGrid.Visibility = Visibility.Visible;
            websiteTextBox.Text = "";
        }

        private void MainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {

        }

        private void Browser_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
            var scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            var size = new Size(bounds.Width * scaleFactor, bounds.Height * scaleFactor);
            Debug.WriteLine("AD"+ScaleRect.ActualWidth + " " + ScaleRect.ActualHeight);

            Debug.WriteLine("SIZE!!!!");
            if (webBrowserDataSource != null)
            {
                var s = e.NewSize;
                s.Width = ScaleRect.ActualWidth;
                s.Height = ScaleRect.ActualHeight;

                webBrowserDataSource.SizeChange(s);
            }

        }
        public bool discovering = false;

        DatagramSocket serverDatagramSocket;

        private void DiscoverBtn_Click(object sender, RoutedEventArgs e)
        {
            
            //TODO:
            //1336 & 1337 for UDP ports, 5454X is out of specon UWP?
            int udpPort = 54545;
            int udpRecPort = 54546;


            ConnectPage.Visibility = Visibility.Collapsed;
            DiscoveryPage.Visibility = Visibility.Visible;

            sendingClient = new UdpClient(udpPort);
            sendingClient.EnableBroadcast = true;


            recivingClient = new UdpClient(udpRecPort);
            


            //3 seconds
            UdpDiscoveryTimer = new Timer(state =>
            {
                try
                {
                    //datagram discovery, we broadcast that we WANT an adress
                    var packet = new DiscoveryPacket
                    {
                        PType = DiscoveryPacketType.AddressRequest,
                    };
                    var rawPacket = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet));
                    sendingClient.SendAsync(rawPacket, rawPacket.Length, new System.Net.IPEndPoint(IPAddress.Parse("255.255.255.255"), udpPort));
                }
                catch (Exception) { }
            }, null, 0, 3000);

            discovering = true;

            serverDatagramSocket = new Windows.Networking.Sockets.DatagramSocket();

            // The ConnectionReceived event is raised when connections are received.
            serverDatagramSocket.MessageReceived += ServerDatagramSocket_MessageReceived;
        
            // Start listening for incoming TCP connections on the specified port. You can specify any port that's not currently in use.
            serverDatagramSocket.BindServiceNameAsync("1337");

        }

        private void ServerDatagramSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            string request;
            using (DataReader dataReader = args.GetDataReader())
            {
                request = dataReader.ReadString(dataReader.UnconsumedBufferLength).Trim();
            }
            Debug.WriteLine(request);

            var packet = JsonConvert.DeserializeObject<DiscoveryPacket>(request);

            switch (packet.PType)
            {
                case DiscoveryPacketType.AddressRequest:
                    break;
                case DiscoveryPacketType.ACK:
                    Debug.WriteLine("ws://" + packet.ServerAddress + ":8081");

                    _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        //replace with a connect function
                        UdpDiscoveryTimer.Dispose();
                        serverDatagramSocket.Dispose();


                        Connect("ws://" + packet.ServerAddress + ":8081", "ws://" + packet.ServerAddress + ":8082");
                        /*
                        ds = new WebBrowserDataSource();
                        ds.DataRecived += (s, o) =>
                        {
                            test.Source = ConvertToBitmapImage(o).Result;
                            // ds.ACKRender();
                        };
                        */
                        ConnectPage.Visibility = Visibility.Collapsed;
                        DiscoveryPage.Visibility = Visibility.Collapsed;
                        NavbarGrid.Visibility = Visibility.Visible;
                        // ds.StartRecive("ws://" + packet.ServerAddress + ":8081");

                    });
                    break;
                default:
                    break;
            }
        }

        private void NavigateBack_Click(object sender, RoutedEventArgs e)
        {
            webBrowserDataSource.NavigateBack();
            TogglePageLoadingMode(true);
        }

        private void NavigateForward_Click(object sender, RoutedEventArgs e)
        {
            webBrowserDataSource.NavigateForward();
            TogglePageLoadingMode(true);
        }

        private void urlViewField_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            ToggleSettingsOverlay(false);
            
            EntryNavBar.Visibility = Visibility.Visible;
            urlField.Focus(FocusState.Programmatic);
            urlField.SelectAll();
            MainNavBar.Visibility = Visibility.Collapsed;
            OverlayFocus.Visibility = Visibility.Visible;
        }

        private void urlField_LostFocus(object sender, RoutedEventArgs e)
        {
            EntryNavBar.Visibility = Visibility.Collapsed;
            MainNavBar.Visibility = Visibility.Visible;
            OverlayFocus.Visibility = Visibility.Collapsed;
        }

        private void ToggleSettingsOverlay(bool state)
        {
            if (!state)
            {
                OverlayMoreSettingsCanvas.Visibility = Visibility.Collapsed;
                var visible = ApplicationView.GetForCurrentView().VisibleBounds;
                Canvas.SetTop(OverlaySettingsLinksGrid, visible.Height - EntryNavBar.Height - OverlaySettingsLinksGrid.Height);

                SettingsLinksGridStoryboardShow.Stop();
                SettingsLinksGridStoryboardHide.Begin();

                OverlayFocus.Visibility = Visibility.Collapsed;
            } 
            else
            {
                OverlayMoreSettingsCanvas.Visibility = Visibility.Visible;
                OverlaySettingsLinks.Visibility = Visibility.Visible;
                var visible = ApplicationView.GetForCurrentView().VisibleBounds;
                Canvas.SetTop(OverlaySettingsLinksGrid, visible.Height - EntryNavBar.Height - MoreSettingsGrid.Height -
                    OverlaySettingsLinksGrid.Height);


                SettingsLinksGridStoryboardHide.Stop();
                SettingsLinksGridStoryboardShow.Begin();

                MoreSettingsGrid.Height = MoreSettingsAppBar.ActualHeight;
                Canvas.SetTop(MoreSettingsGrid, visible.Height - EntryNavBar.Height - MoreSettingsGrid.Height);

                OverlayFocus.Visibility = Visibility.Visible;
            }
        }

        private void More_Click(object sender, RoutedEventArgs e)
        {
            if (OverlayMoreSettingsCanvas.Visibility == Visibility.Visible)
            {
                ToggleSettingsOverlay(false);
            }
            else
            {
                ToggleSettingsOverlay(true);
            }
        }

        private void ToggleMode(bool? showScreenshot)
        {
            if (showScreenshot == true)
            {
                ScreenshotScrollViewer.Visibility = Visibility.Visible;
                test.Visibility = Visibility.Collapsed;
                webBrowserDataSource.StaticUpdateMode = true;
            }
            else
            {
                ScreenshotScrollViewer.Visibility = Visibility.Collapsed;
                test.Visibility = Visibility.Visible;
                webBrowserDataSource.StaticUpdateMode = false;
            }
        }

        private void StaticModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool isStaticMode = StaticModeToggleButton.IsChecked == true;

            var mode = isStaticMode ? "Static" : "Dynamic";
            webBrowserDataSource.SendModeChange(mode);

            ToggleMode(isStaticMode);

        }

        private void SettingsLinksGridHide_Completed(object sender, object e)
        {
            OverlaySettingsLinks.Visibility = Visibility.Collapsed;
        }

        private void OpenTabsPage()
        {
            if (tabs.Count == 0)
            {
                webBrowserDataSource.SendGetTabsRequest();
            }
            if (tabs.Count > 0)
            {
                if (activeTabId != 0)
                {
                    _ = UpdateScreenshotAsync(activeTabId);

                }
            }
            TabsGrid.Visibility = Visibility.Visible;
            browser.Visibility = Visibility.Collapsed;
            TabsList.UpdateLayout();
            NavBarStoryboardShow.Stop();
            NavBarStoryboardHide.Begin();
            SetupTabWidth();
        }

        private void ViewPages_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsOverlay(false);
            if (TabsGrid.Visibility == Visibility.Visible)
            {
                TabsGrid.Visibility = Visibility.Collapsed;
                browser.Visibility = Visibility.Visible;
            }
            else
            {
                OpenTabsPage();
            }
        }

        private void OverlayFocus_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            ToggleSettingsOverlay(false);
            EntryNavBar.Visibility = Visibility.Collapsed;
            MainNavBar.Visibility = Visibility.Visible;
        }

        private void HomePageButton_Click(object sender, RoutedEventArgs e)
        {
            string url = "google.com/";
            urlViewField.Text = url;
            webBrowserDataSource.Navigate(url);
            ToggleSettingsOverlay(false);
            TogglePageLoadingMode(true);
        }

        public void TogglePageLoadingMode(bool state)
        {
            if (state)
            {
                PageLoadingRing.Visibility = Visibility.Visible;
                PageLoadingRing.IsActive = true;
                ConnectionPreviewButton.Visibility = Visibility.Collapsed;
            } 
            else
            {
                PageLoadingRing.Visibility = Visibility.Collapsed;
                PageLoadingRing.IsActive = false;
                ConnectionPreviewButton.Visibility = Visibility.Visible;

            }
        }
        private ObservableCollection<TabItem> tabs = new ObservableCollection<TabItem>();
        public class TabItem : INotifyPropertyChanged
        {
            private string title;

            public long Id { get; set; }

            public string Url { get; set; }

            public string Title
            {
                get => title;
                set
                {
                    if (title != value)
                    {
                        title = value;
                        OnPropertyChanged(nameof(Title));
                    }
                }
            }
            private BitmapImage screenshot;

            public BitmapImage Screenshot 
            {
                get => screenshot;
                set
                {
                    if (screenshot != value)
                    {
                        screenshot = value;
                        OnPropertyChanged(nameof(Screenshot));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        private void TabsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedTab = e.ClickedItem as TabItem;
            if (clickedTab != null)
            {
                webBrowserDataSource.SetActivePage(clickedTab.Id);
                activeTabId = clickedTab.Id;

                _ = UpdateScreenshotAsync(clickedTab.Id);
                TabsPanelHide();
                var visible = ApplicationView.GetForCurrentView().VisibleBounds;
                string size = $"{visible.Width}x{visible.Height - NavbarGrid.Height}";
                webBrowserDataSource.RequestNewScreenshot(size);
            }
        }

        private void TabsPanelHide()
        {
            TabsGrid.Visibility = Visibility.Collapsed;
            browser.Visibility = Visibility.Visible;
            NavbarGrid.Visibility = Visibility.Visible;
            NavBarStoryboardHide.Stop();
            NavBarStoryboardShow.Begin();
        }

        private async void UpdateTabsGrid(List<string> urls)
        {
            tabs.Clear();
            StateHelper.Instance.OpenedTabsId.Clear();

            foreach (var url in urls)
            {
                var splitUrl = url.Split('|').ToList();
                Int64.TryParse(splitUrl[1], out long id);
                var title = splitUrl[0];
                string _url = "NaN";
                if (splitUrl.Count == 3)
                {
                    _url = splitUrl[2];
                }
                tabs.Add(new TabItem
                {
                    Id = id,
                    Url = _url,
                    Title = title,
                    Screenshot = await GetScreenshotForUrl(id), 
                });
                StateHelper.Instance.OpenedTabsId.Add(id);
                if (_url != "NaN")
                {
                    await RecentTabsManager.AddRecentTabAsync(new RecentTabInfo
                    {
                        Id = id,
                        Url = _url,
                        Title = title,
                        ClosedAt = DateTime.UtcNow
                    });
                }
            }
            TabsList.ItemsSource = tabs;

            TabsList.UpdateLayout();

            foreach (var item in TabsList.Items)
            {
                var container = TabsList.ContainerFromItem(item) as GridViewItem;
                if (container != null)
                {
                    var border = FindChild<Border>(container);
                    if (border != null)
                    {
                        border.Width = DefaultTabWidth;
                        border.Height = DefaultTabWidth;
                    }
                }
            }
        }

        private async Task<BitmapImage> GetScreenshotForUrl(long tabId)
        {
            try
            {

                var folder = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFolderAsync("TabsScreenshots");
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
                return new BitmapImage(new Uri("ms-appx:///Assets/placeholder.png"));
            }
        }

        private async Task UpdateScreenshotAsync(long tabId)
        {
            if (screenshotUpdateQueue.ContainsKey(tabId))
            {
                return; 
            }

            var tcs = new TaskCompletionSource<bool>();
            screenshotCompletionSources[tabId] = tcs;

            var updateTask = Task.Run(async () =>
            {
                try
                {
                    webBrowserDataSource.RequestTabScreenshot(tabId);

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;

                    if (completed)
                    {

                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                        {
                            for (int i = 0; i < tabs.Count; i++)
                            {
                                if (tabs[i].Id == tabId)
                                {
                                    Debug.WriteLine($"Updating image for tab {tabId}");
                                    tabs[i].Screenshot = await GetScreenshotForUrl(tabId);
                                }
                            }
                        });
                    }
                    else
                    {
                        Debug.WriteLine($"Timeout while waiting for screenshot of tab {tabId}");
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Unexpected error: {e}");
                }
                finally
                {
                    screenshotCompletionSources.Remove(tabId);
                }
            });

            screenshotUpdateQueue[tabId] = updateTask;

            try
            {
                await updateTask;
            }
            finally
            {
                screenshotUpdateQueue.Remove(tabId);
            }
        }

        private void CloseTabsPageButton_Click(object sender, RoutedEventArgs e)
        {
            TabsPanelHide();

        }

        private void SettingsPageButton_Click(object sender, RoutedEventArgs e)
        {
            OverlayFocus.Visibility = Visibility.Collapsed;
            Frame.Navigate(typeof(SettingsPage));
        }

        private void NavBarHide_Completed(object sender, object e)
        {
            NavbarGrid.Visibility = Visibility.Collapsed;
        }

        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            if (button == null)
                return;

            var item = button.DataContext as TabItem; 

            if (item != null)
            {
                _ = RecentTabsManager.RemoveRecentTabAsync(item.Id);
                webBrowserDataSource.CloseTab(item.Id);
                tabs.Remove(item);
            }
        }

        private void AddNewTabButton_Click(object sender, RoutedEventArgs e)
        {
            TabsPanelHide();
            webBrowserDataSource.CreateNewTabWithUrl(defaultNewPageUrl);
        }
        private async Task ShowErrorDialogAsync(string title, string message)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
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

        public async void OpenTabFromRecent(RecentTabInfo tab)
        {
            webBrowserDataSource.CreateNewTabWithUrl(tab.Url);
            if (!tab.IsPinned)
                await RecentTabsManager.RemoveRecentTabAsync(tab.Id);
        }

        public List<long> GetOpenTabIds()
        {
            return tabs.Select(t => t.Id).ToList();
        }

        private void RecentButton_Click(object sender, RoutedEventArgs e)
        {
            string featureName = "RECENT";
            if (StateHelper.Instance.AvailableFeatures == null ||
                !StateHelper.Instance.AvailableFeatures.Contains(featureName))
            {
                var formatString = resourceLoader.GetString("FeatureNotSupported");
                var text = string.Format(formatString, featureName, "1.0.1.0");
                _ = ShowErrorDialogAsync(
                    resourceLoader.GetString("FeatureNotSupportedTitle"),
                   text
                    );
            }
            else if (!(bool)localSettings.Values["UseRecentFeature"])
            {
                _ = ShowErrorDialogAsync(
                    resourceLoader.GetString("FeatureDisabledTitle"),
                    string.Format(resourceLoader.GetString("FeatureDisabled"), featureName)
                    );
            }
            else
            {
                Frame.Navigate(typeof(LinesBrowser.Pages.RecentTabsPage));
            }
        }

        private void OverlayInvisibleFocus_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {

        }

        private void PopulateInfoGrid(ConnectionSecurePacket data)
        {
            CertViewButton.Visibility = Visibility.Visible;
            InfoGrid.RowDefinitions.Clear();
            InfoGrid.Children.Clear();

            string issuerDN = data.Issuer;
            Dictionary<string, string> dnParts = CertHelper.ParseDistinguishedName(issuerDN);

            string Country = dnParts.ContainsKey("C") ? dnParts["C"] : string.Empty;
            string Organization = dnParts.ContainsKey("O") ? dnParts["O"] : string.Empty;
            string IssuerOrgUnit = dnParts.ContainsKey("OU") ? dnParts["OU"] : string.Empty;
            string Name = dnParts.ContainsKey("CN") ? dnParts["CN"] : string.Empty;
            string IssuerLocality = dnParts.ContainsKey("L") ? dnParts["L"] : string.Empty;
            string IssuerState = dnParts.ContainsKey("ST") ? dnParts["ST"] : string.Empty;
            string IssuerEmail = dnParts.ContainsKey("E") ? dnParts["E"] : string.Empty;

            IssuerNameTextBlock.Text = Organization;

            int row = 0;
            CertURLTextBlock.Text = data.Url;

            CertErrorTextBlock.Text = resourceLoader.GetString("No/Content");
            CertCorrectText.Text = resourceLoader.GetString("Correct");
            CertCorrectIcon.Glyph = "\uE930";

            SecureTextBlock.Text = resourceLoader.GetString("Insecure");
            SecureFontIcon.Glyph = "\uE785";

            ServerConnectionTextBlock.Text = resourceLoader.GetString("Insecure");

            ConnectionInfoTextBlock.Text = resourceLoader.GetString("ConnectionServerSecureError");

            ConnectionStatusFontIcon.Glyph = "";

            if (data.IsSecureConnection)
            {
                SecureTextBlock.Text = resourceLoader.GetString("Secure");
                SecureFontIcon.Glyph = "\uE72E";
            }

            if (data.CertificateError)
            {
                CertErrorTextBlock.Visibility = Visibility.Visible;
                CertErrorTextBlock.Text = data.CertificateErrorName;
                CertCorrectText.Text = resourceLoader.GetString("Incorrect");
                CertCorrectIcon.Glyph = "\uEA39";
                SecureTextBlock.Text = resourceLoader.GetString("Insecure");
                SecureFontIcon.Glyph = "\uE785";
                ConnectionStatusFontIcon.Glyph = "\uE7BA";
                ConnectionInfoTextBlock.Text = resourceLoader.GetString("ConnectionCertSecureError");

            }


            CertTslTextBlock.Text = data.TlsVersion;


            AddPairRow($"{resourceLoader.GetString("Name")}:", SafeString(data.Subject).Replace("CN=", ""), ref row);

            AddPairRow($"{resourceLoader.GetString("Country")} (C):", SafeString(Country), ref row);
            AddPairRow($"{resourceLoader.GetString("Organization")} (O):", SafeString(Organization), ref row);
            AddPairRow($"{resourceLoader.GetString("OrganizationalUnit")} (OU):", SafeString(IssuerOrgUnit), ref row);
            AddPairRow($"{resourceLoader.GetString("Name")} (CN):", SafeString(Name), ref row);
            AddPairRow($"{resourceLoader.GetString("Locality")} (L):", SafeString(IssuerLocality), ref row);

            AddPairRow($"{resourceLoader.GetString("ValidFrom")}:", data.ValidFromTime == DateTime.MinValue ? "-" : data.ValidFromTime.ToString("yyyy-MM-dd HH:mm:ss"), ref row);
            AddPairRow($"{resourceLoader.GetString("ValidTo")}:", data.ValidToTime == DateTime.MinValue ? "-" : data.ValidToTime.ToString("yyyy-MM-dd HH:mm:ss"), ref row);

            AddPairRow($"Thumbprint:", SafeString(data.Thumbprint), ref row);
            AddPairRow($"Serial No:", SafeString(data.SerialNumber), ref row);
            AddPairRow($"Public Key:", SafeString(data.PublicKey), ref row);
        }

        private string SafeString(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? resourceLoader.GetString("NotInCert") : s;
        }

        private void AddSectionHeader(string headerText, ref int rowIndex)
        {
            InfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });


            var header = new TextBlock
            {
                Text = headerText,

                FontSize = 16,
                Margin = new Thickness(0, 10, 0, 5) 
            };

            Grid.SetRow(header, rowIndex);
            Grid.SetColumn(header, 0);
            Grid.SetColumnSpan(header, 3);

            InfoGrid.Children.Add(header);

            rowIndex++;
        }

        private void AddPairRow(string label, string value, ref int rowIndex)
        {
            InfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tbLabel = new TextBlock
            {
                Text = label,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(tbLabel, rowIndex);
            Grid.SetColumn(tbLabel, 0);
            InfoGrid.Children.Add(tbLabel);

            var tbValue = new TextBlock
            {
                IsTextSelectionEnabled = true,
                Text = value,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(tbValue, rowIndex);
            Grid.SetColumn(tbValue, 2);
            InfoGrid.Children.Add(tbValue);

            rowIndex++;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CertGrid.Visibility = Visibility.Collapsed;
        }

        private void CertViewButton_Click(object sender, RoutedEventArgs e)
        {
            CertGrid.Visibility = Visibility.Visible;
            CertFlyout.Hide();
        }
    }
}
