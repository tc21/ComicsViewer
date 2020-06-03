using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace ComicsViewer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {

        /*
         * TODOS:
         * - Working search box
         * - Right click to open, new page with selected, etc.
         * - Expansion panel on single click i.e. the entire reason for this rewrite
         * - Which in turn will enable subworks and alternative works
         * - (Optional) Reimplement the original sidebar?
         * - Properly layout the sort order box
         * - Supporting opening stuff properly, rathor than telling Windows to open the first file 
         * - Allow for importing libraries between this version and the WPF version
         *   (see https://stackoverflow.com/a/44006005 or https://docs.microsoft.com/en-us/windows/uwp/winrt-components/brokered-windows-runtime-components-for-side-loaded-windows-store-apps)
         * - We will likely have to rely on the above two links to enable Python-based extensions.
         * - (Low priority for now, since we have the WPF version) allow writing to the database and all the features that come with it
         * 
         * Feature Requests:
         * - When clicking a navigation header, if we are already on that page, instead of loading a new copy of the page, scroll to the top
         * - Grouping, and the ability to navigate to the start of a group (see Groove Music) (also this is apparently really hard)
         * - Reimplement the faded colors of the WPF version, ideally as a togglable setting
         * - To enable the above feature, we will need a settings pane for App-level (instead of and in addition to profile-level) settings.
         * - To enable progress tracking, we will probably need to implement a UWP-based image viewer too.
         *   This time we probably won't have to make it general-purpose.
         * - Ideally, we should be able to browse search results by category/author/tag too, but this is low priority.
         */
        public static readonly Random Randomizer = new Random();

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
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
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
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
