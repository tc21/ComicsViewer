using System;
using System.Linq;
using ComicsViewer.Common;
using ComicsViewer.Uwp.Common;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace MusicPlayer {
    public sealed partial class MainPage {
        private ViewModel ViewModel { get; } = new();

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
            if (e.Parameter is ProtocolActivatedArguments args) {
                switch (args.Mode) {
                    case ProtocolActivatedMode.Filenames:
                        await this.ViewModel.OpenFilesAtPathAsync(args.Filenames!);
                        break;
                    case ProtocolActivatedMode.Folder:
                        await this.ViewModel.OpenFolderAsync(args.Folder!);
                        break;
                    case ProtocolActivatedMode.File:
                        await this.ViewModel.OpenContainingFolderAsync(args.File!);
                        break;
                    default:
                        throw new ProgrammerError("unhandled switch case");
                }

                this.ViewModel.CurrentDescription = args.Description;
            }

            this.NavigationView.SelectedItem = this.NavigationView.MenuItems[0];
            var navArgs = new PlaylistPageNavigationArguments(this.ViewModel.PlaylistItems, this.ViewModel);
            _ = this.NavigationViewContent.Navigate(typeof(PlaylistPage), navArgs);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(250, this.AppTitleBar.ActualHeight + this.Player.ActualHeight));
        }

        private async void MediaPlayer_MediaEnded(Windows.Media.Playback.MediaPlayer sender, object? args) {
            sender.MediaEnded -= this.MediaPlayer_MediaEnded;
            sender.Pause();

            if (this.ViewModel.Next() is { } item) {
                await this.ViewModel.PlayAsync(item);
            }
        }

        private void OurMediaTransportControlsHack_NextTrackClicked(MediaTransportControls sender, RoutedEventArgs args) {
            this.MediaPlayer_MediaEnded(this.Player.MediaPlayer, null);
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
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) {
                return;
            }

            var items = (await e.DataView.GetStorageItemsAsync()).InNaturalOrder();

            if (items.Count == 1) {
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

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args) {
            switch (args.InvokedItem) {
                case "Playlist":
                    var navArgs = new PlaylistPageNavigationArguments(this.ViewModel.PlaylistItems, this.ViewModel);
                    _ = this.NavigationViewContent.Navigate(typeof(PlaylistPage), navArgs);
                    break;

                case "Info":
                    _ = this.NavigationViewContent.Navigate(typeof(InfoPage), this.ViewModel);
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

    /* The "correct" way of enabling the forward/previous buttons is to use a MediaPlaybackList. However, that
     * overly complicates the logic of PlaylistPage, so we use this hack for now. */
    public class OurMediaTransportControlsHack : MediaTransportControls {
        protected override void OnApplyTemplate() {
            if (this.GetTemplateChild("NextTrackButton") is Button nextTrack) {
                nextTrack.Click += this.NextTrack_Click;
            }

            base.OnApplyTemplate();
        }

        private void NextTrack_Click(object sender, RoutedEventArgs e) {
            this.NextTrackClicked?.Invoke(this, e);
        }

        public event Action<MediaTransportControls, RoutedEventArgs>? NextTrackClicked;
    }
}
