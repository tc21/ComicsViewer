using System;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.Uwp.Common;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace ImageViewer {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
        }

        private async Task StopAppLaunch(string title, string message) {
            Window.Current.Activate();
            await Task.Delay(100);
            _ = await new ContentDialog {
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            }.ShowAsync();
        }

        protected override async void OnActivated(IActivatedEventArgs args) {
            if (args.Kind == ActivationKind.Protocol) {
                var rootFrame = this.EnsureInitialized();

                if (!(args is ProtocolActivatedEventArgs eventArgs)) {
                    return;
                }

                var parsed = await Helper.ParseActivationArguments(eventArgs);

                if (parsed.Result != ProtocolActivatedResult.Success) {
                    await this.StopAppLaunch(parsed.Result.Description(), parsed.ErrorMessage ?? "An error occurred");
                    return;
                }

                _ = rootFrame.Navigate(typeof(MainPage), parsed);
                Window.Current.Activate();
            }
        }

        protected override async void OnFileActivated(FileActivatedEventArgs args) {
            var rootFrame = this.EnsureInitialized();

            if (args.Files.OfType<StorageFolder>().Any()) {
                await this.StopAppLaunch("Not supported", "we cannot open folders");
                return;
            }

            var files = args.Files.OfType<StorageFile>().ToList();

            if (files.Count() != 1) {
                await this.StopAppLaunch("Not supported", "we only allow opening individual images");
                return;
            }

            _ = rootFrame.Navigate(typeof(MainPage), new ProtocolActivatedArguments {
                Mode = ProtocolActivatedMode.File,
                File = files.First()
            });

            Window.Current.Activate();
        }

        private Frame EnsureInitialized() {
            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (!(Window.Current.Content is Frame rootFrame)) {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += this.OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            return rootFrame;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e) {
            var rootFrame = this.EnsureInitialized();

            if (e.PreviousExecutionState == ApplicationExecutionState.Terminated) {
                //TODO: Load state from previously suspended application
            }

            if (e.PrelaunchActivated == false) {
                if (rootFrame.Content == null) {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    _ = rootFrame.Navigate(typeof(MainPage), e.Arguments);
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
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e) {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e) {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
