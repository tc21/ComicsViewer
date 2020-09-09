using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using ComicsViewer.Common;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace MusicPlayer {
    public sealed partial class MainPage : Page {
        private readonly ViewModel ViewModel = new ViewModel();

        public MainPage() {
            this.InitializeComponent();

            // App size
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(250, 0));

            // Custom title bar
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            coreTitleBar.LayoutMetricsChanged += this.CoreTitleBar_LayoutMetricsChanged;

            // Transparent upper-right-area buttons
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Windows.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Windows.UI.Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 255, 255, 255);

            this.ViewModel.PlayRequested += this.ViewModel_PlayRequested;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            switch (e.Parameter) {
                case IEnumerable<StorageFile> files:
                    await this.ViewModel.OpenFilesAsync(files);
                    break;
                case StorageFolder folder:
                    await this.ViewModel.OpenFolderAsync(folder);
                    break;
                case StorageFile file:
                    await this.ViewModel.OpenContainingFolderAsync(file);
                    break;
                default:
                    return;
            }

            this.NavigationView.SelectedItem = this.NavigationView.MenuItems[0];
            var navArgs = new PlaylistPageNavigationArguments(this.ViewModel.PlaylistItems, this.ViewModel);
            _ = this.NavigationViewContent.Navigate(typeof(PlaylistPage), navArgs);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(250, this.AppTitleBar.ActualHeight + this.Player.ActualHeight));
        }

        private async void MediaPlayer_MediaEnded(Windows.Media.Playback.MediaPlayer sender, object args) {
            sender.MediaEnded -= this.MediaPlayer_MediaEnded;
            sender.Pause();

            if (this.ViewModel.Next() is PlaylistItem item) {
                await this.ViewModel.PlayAsync(item);
            }
        }

        private void ViewModel_PlayRequested(ViewModel sender, MediaSource source) {
            this.Player.Source = source;
            this.Player.MediaPlayer.MediaEnded += this.MediaPlayer_MediaEnded;
            this.Player.MediaPlayer.Play();
        }

        private void Player_DragOver(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Player_Drop(object sender, DragEventArgs e) {
            if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
                var items = await e.DataView.GetStorageItemsAsync();

                if (items.Count() == 1) {
                    var item = items.First();
                    if (item.IsOfType(StorageItemTypes.File)) {
                        await this.ViewModel.OpenContainingFolderAsync((StorageFile)item);
                    } else {
                        await this.ViewModel.OpenFolderAsync((StorageFolder)item);
                    }
                } else {
                    await this.ViewModel.OpenFilesAsync(items.OfType<StorageFile>());
                }

                this.NavigationView.SelectedItem = this.NavigationView.MenuItems[0];
                var navArgs = new PlaylistPageNavigationArguments(this.ViewModel.PlaylistItems, this.ViewModel);
                _ = this.NavigationViewContent.Navigate(typeof(PlaylistPage), navArgs);
            }
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args) {
            switch (args.InvokedItem) {
                case "Playlist":
                    var navArgs = new PlaylistPageNavigationArguments(this.ViewModel.PlaylistItems, this.ViewModel);
                    _ = this.NavigationViewContent.Navigate(typeof(PlaylistPage), navArgs);
                    break;

                case "Info":
                    
                    break;

                default:
                    throw new ProgrammerError("unhandled switch case");
            }
        }

        /* reference: https://docs.microsoft.com/en-us/windows/uwp/design/shell/title-bar#full-customization-example */
        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args) {
            this.LeftPaddingColumn.Width = new GridLength(sender.SystemOverlayLeftInset);
            this.RightPaddingColumn.Width = new GridLength(sender.SystemOverlayRightInset);

            this.AppTitleBar.Height = sender.Height;
        }
    }
}
