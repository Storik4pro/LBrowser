﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Globalization;
using Windows.Storage;
using LinesBrowser.Managers;

namespace LinesBrowser
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        public LinesBrowser.MainPage MainPageInstance { get; set; }
        Frame rootFrame = Window.Current.Content as Frame;
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(200, 200));

            string savedLang = localSettings.Values["AppLanguage"] as string;
            if (!string.IsNullOrEmpty(savedLang))
            {
                ApplicationLanguages.PrimaryLanguageOverride = savedLang;
            }
            else
            {

            }
            if (localSettings.Values["UseRecentFeature"] is null)
            {
                localSettings.Values["UseRecentFeature"] = true;
            }

            if (!(bool)localSettings.Values["UseRecentFeature"])
            {
                _ = RecentTabsManager.DeleteAllUnusedScreenshots();
            }

            ConnectionHelper.Instance.OnDisconnected += ConnectionHelper_OnDisconnected;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            rootFrame.Navigated += (s, args) =>
            {
                if (args.Content is LinesBrowser.MainPage mp)
                    this.MainPageInstance = mp;
            };

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    if (localSettings.Values["AutoConnect"] is bool autoConnect && autoConnect)
                    {
                        string serverAddress = (string)localSettings.Values["serverAddress"];
                        bool? enableAudioStream = (bool?)localSettings.Values["EnableAudioStream"];
                        string audioServerAddress = (string)localSettings.Values["audioServerAddress"];

                        if (enableAudioStream == null || (bool)!enableAudioStream)
                            audioServerAddress = null;

                        if (serverAddress==null || serverAddress == String.Empty)
                            rootFrame.Navigate(typeof(ConnectPage), e.Arguments);
                        else
                            rootFrame.Navigate(typeof(ConnectProgressPage), Tuple.Create(ConnectProgressPage.State.Basic, serverAddress, audioServerAddress, ""));
                    }
                    else
                    {
                        rootFrame.Navigate(typeof(ConnectPage), e.Arguments);
                    }
                }
                // Ensure the current window is active
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        private void ConnectionHelper_OnDisconnected(object sender, string errorCode)
        {
            string serverAddress = (string)localSettings.Values["serverAddress"];
            bool? enableAudioStream = (bool?)localSettings.Values["EnableAudioStream"];
            string audioServerAddress = (string)localSettings.Values["audioServerAddress"];

            if (enableAudioStream == null || (bool)!enableAudioStream)
                audioServerAddress = null;

            rootFrame.Navigate(
                typeof(ConnectProgressPage), 
                Tuple.Create(
                    ConnectProgressPage.State.DisconnectedWithError, 
                    serverAddress, 
                    audioServerAddress,
                    errorCode
                    )
                );
        }
    }
}
