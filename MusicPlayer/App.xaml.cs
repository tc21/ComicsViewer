﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
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

namespace MusicPlayer {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        protected override async void OnActivated(IActivatedEventArgs args) {
            if (args.Kind == ActivationKind.Protocol) {
                var rootFrame = this.EnsureInitialized();

                var eventArgs = args as ProtocolActivatedEventArgs;

                if (eventArgs.Uri.AbsolutePath == "/files" && eventArgs.Data.TryGetValue("FirstFileToken", out var o) && o is string token) {
                    var file = await SharedStorageAccessManager.RedeemTokenForFileAsync(token);
                    _ = rootFrame.Navigate(typeof(MainPage), file);
                    Window.Current.Activate();
                    return;
                }

                if (eventArgs.Uri.AbsolutePath == "/path" && eventArgs.Data.TryGetValue("Path", out var p) && p is string path) {
                    try {
                        try {
                            var s = await StorageFolder.GetFolderFromPathAsync(path);
                            _ = rootFrame.Navigate(typeof(MainPage), s);
                            return;
                        } catch (ArgumentException) {
                            /* fall through */
                        } catch (FileNotFoundException) {
                            /* fall through */
                        }

                        var f = await StorageFile.GetFileFromPathAsync(path);
                        _ = rootFrame.Navigate(typeof(MainPage), f);
                        Window.Current.Activate();
                        return;
                    } catch (Exception e) {
                        Window.Current.Activate();
                        await Task.Delay(100);
                        _ = new ContentDialog {
                            Title = "An exception occured",
                            Content = e.ToString(),
                            CloseButtonText = "OK"
                        }.ShowAsync();
                    }
                }

                Window.Current.Activate();
                await Task.Delay(100);
                _ = new ContentDialog {
                    Title = "Failed to parse launch uri",
                    Content = $"The application could not parse the launch uri.\n" +
                    $"Url.AbsolutePath = {eventArgs.Uri.AbsolutePath}\n" +
                    $"Data[FirstFileToken] = {(eventArgs.Data.TryGetValue("FirstFileToken", out var t) ? t : "<unassigned>")}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        protected override void OnFileActivated(FileActivatedEventArgs args) {
            var rootFrame = this.EnsureInitialized();

            _ = rootFrame.Navigate(typeof(MainPage), args.Files.OfType<StorageFile>());
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
