using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using PhenoPad.LogService;
using Windows.System;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using Windows.UI;

namespace PhenoPad
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private bool _isinBackground;
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            //Binding event handlers to handle app status
            {

                // Subscribe to key lifecyle events to know when the app
                // transitions to and from foreground and background.
                // Leaving the background is an important transition
                // because the app may need to restore UI.
                EnteredBackground += AppEnteredBackground;
                LeavingBackground += AppLeavingBackground;
            }
        }

        private void AppLeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            MetroLogger.getSharedLogger().Info("App leaved background.");
            _isinBackground = false;
        }

        private void AppEnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            MetroLogger.getSharedLogger().Info("App entered background.");
            _isinBackground = true;
        }

        /// <summary>
        /// Invoked when Application receives an unhandled exception
        /// </summary>
        private static async void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {           
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                async () =>
                {
                    await ShowErrorDialog(e.Message);
                    MetroLogger.getSharedLogger().Error($"APP Unhandled exception at {sender.ToString()}:\n {e.Message}");
                    e.Handled = true;
                });          
        }

        private static async Task<bool> ShowErrorDialog(string message)
        {
            var dialog = new MessageDialog("Error: " + message);
            dialog.Commands.Add(new UICommand("OK") { Id = 0 });
            await dialog.ShowAsync();
            return false;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            this.Suspending += OnSuspending;
            this.UnhandledException += OnUnhandledException;

            Frame rootFrame = Window.Current.Content as Frame;

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

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter

                    rootFrame.Navigate(typeof(PageOverview), e.Arguments);
                }
                // Ensure the current window is active
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
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            //entering background will by default suspend app, we only need to
            //handle suspensions that happened when app is in use.
            if (!_isinBackground) {
                MetroLogger.getSharedLogger().Info($"App is suspended.");
                var deferral = e.SuspendingOperation.GetDeferral();
                if (MainPage.Current != null)
                    await MainPage.Current.saveNoteToDisk();
                deferral.Complete();
            }
        }


        private void SaveAppData()
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;
            Task<StorageFile> tFile = folder.CreateFileAsync("AppData.txt").AsTask<StorageFile>();
            tFile.Wait();
            StorageFile file = tFile.Result;
            Task t = FileIO.WriteTextAsync(file, "This Is Application data").AsTask();
            t.Wait();
        }
        
    }
}
