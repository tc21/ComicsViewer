using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComicsViewer.Common;
using ComicsViewer.Uwp.Common;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ImageViewer {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage {
        private ViewModel ViewModel { get; } = new ViewModel();

        public MainPage() {
            this.InitializeComponent();
            this.ViewModel.PropertyChanged += this.ViewModel_PropertyChanged;

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

            var coreWindow = CoreWindow.GetForCurrentThread();
            coreWindow.KeyDown += this.MainPage_KeyDown;
            coreWindow.KeyUp += this.MainPage_KeyUp;
            coreWindow.ResizeStarted += this.MainPage_ResizeStarted;
            coreWindow.ResizeCompleted += this.MainPage_ResizeCompleted;

            // Zoom level indicator
            this.ImageContainer.ViewChanged += this.ImageContainer_ViewChanged;
            this.ImageContainer.ViewChanging += this.ImageContainer_ViewChanging;
        }

        private void ImageContainer_Loaded(object sender, RoutedEventArgs e) {
            // Reduce moire
            if (Settings.Get(Settings.ScalingEnabledProperty, true)) {
                this.ToggleScalingFlyoutItem.IsChecked = true;
                this.ToggleScalingCommand.Execute(null);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is ProtocolActivatedArguments args) {
                switch (args.Mode) {
                    case ProtocolActivatedMode.Filenames:
                        await this.ViewModel.LoadImagesAtPathsAsync(args.Filenames!);
                        break;
                    case ProtocolActivatedMode.Folder:
                        var files = await args.Folder!.GetFilesInNaturalOrderAsync();
                        await this.ViewModel.LoadImagesAsync(files);
                        break;
                    case ProtocolActivatedMode.File:
                        await this.ViewModel.OpenContainingFolderAsync(args.File!);
                        break;
                    default:
                        throw new ProgrammerError("unhandled switch case");
                }
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(this.ViewModel.CurrentImagePath):
                    foreach (var command in RelayCommand.CreatedCommands) {
                        command.OnCanExecuteChanged();
                    }
                    break;
            }
        }

        private async void Page_PointerWheelChanged(object sender, PointerRoutedEventArgs e) {
            if (e.KeyModifiers != Windows.System.VirtualKeyModifiers.None) {
                return;
            }

            if (e.GetCurrentPoint(this).Properties.MouseWheelDelta > 0) { // This means scrolled up
                await this.ViewModel.SeekAsync(this.ViewModel.CurrentImageIndex - 1);
            } else {
                await this.ViewModel.SeekAsync(this.ViewModel.CurrentImageIndex + 1);
            }

            e.Handled = true;
        }

        #region Dropping

        private void Grid_DragOver(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Grid_Drop(object sender, DragEventArgs e) {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) {
                return;
            }

            var items = (await e.DataView.GetStorageItemsAsync()).InNaturalOrder();

            if (items.Count == 1) {
                if (!(items.First() is StorageFile file)) {
                    return;
                }

                await this.ViewModel.OpenContainingFolderAsync(file);
            } else {
                var files = items.Where(item => item.IsOfType(StorageItemTypes.File))
                                 .Cast<StorageFile>();

                await this.ViewModel.LoadImagesAsync(files);
            }
        }

        #endregion

        #region Zooming

        // For some reason, you have to wait a while before calling ChangeView
        private const int ChangeViewDelay = 20;  // milliseconds

        private void ResetZoom() {
            _ = new Timer(async __ => await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                _ = this.ImageContainer.ChangeView(0, 0, 1))
            , null, ChangeViewDelay, Timeout.Infinite);
        }

        private void ZoomImage(double scale) {
            // For some reason, you have to wait a while before calling ChangeView
            _ = new Timer(async __ => await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                /* Although ChangeView automatically constrains zooms to MaxZoomFactor, we need the accurate value for our calculations below.
                 * We don't need to do this for MinZoomFactor, because the image is already forced to be centered in that case. */
                var zoomTo = this.ImageContainer.ZoomFactor * (float)scale;

                if (zoomTo > this.ImageContainer.MaxZoomFactor) {
                    zoomTo = this.ImageContainer.MaxZoomFactor;
                }

                // we use these imprecise numbers because floats are imprecise
                if ((zoomTo > 1 && this.ImageContainer.ZoomFactor < 0.999) || (zoomTo < 1 && this.ImageContainer.ZoomFactor > 1.001)) {
                    zoomTo = 1;
                }

                if (Math.Abs(zoomTo - 1) < 0.05) {
                    zoomTo = 1;
                }

                scale = zoomTo / this.ImageContainer.ZoomFactor;
                
                /* zoomOriginX is the origin of the zoom relative to (0, 0), in the extent coordinate space
                 * (i.e. if a 100x100 image is zoomed at 200%, then ExtentSize = 200x200, while ActualSize = 100x100
                 * 
                 * If you zoom in that image by 10% more (200 -> 220%), centered at (100, 100) in extend space,
                 * then you have to scroll down and right by ExtendOrigin / ExtentSize * 10%, which in this case would be (10, 10) */
                var zoomOriginX = this.ImageContainer.HorizontalOffset + (this.ImageContainer.ActualWidth / 2);
                var zoomOriginY = this.ImageContainer.VerticalOffset + (this.ImageContainer.ActualHeight / 2);

                // if you zoom in a 1000x1000 image by 10%, centered at (500,500) then we have to scroll each item left and topwards by 50px.
                var widthDifference = zoomOriginX * (1 - scale);
                var heightDifference = zoomOriginY * (1 - scale);

                _ = this.ImageContainer.ChangeView(
                    this.ImageContainer.HorizontalOffset - widthDifference,
                    this.ImageContainer.VerticalOffset - heightDifference,
                    zoomTo
                );
            }), null, ChangeViewDelay, Timeout.Infinite);
        }

        private const int InteractionDelay = 100;

        // We could alternatively use converters and implement INotifyPropertyChanged, but not for just one text block
        private void ImageContainer_ViewChanged(object sender, Windows.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e) {
            this.ZoomFactorTextBlock.Text = (100 * this.ImageContainer.ZoomFactor).ToString("N0") + "%";
            this.ZoomFactorButton.Visibility = Math.Abs(1 - this.ImageContainer.ZoomFactor) < 0.001
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ImageContainer_ViewChanging(object sender, Windows.UI.Xaml.Controls.ScrollViewerViewChangingEventArgs e) {
            this.UpdateDecodeImageHeight(e.FinalView.ZoomFactor);
        }

        private void ZoomFactorButton_Click(object sender, RoutedEventArgs e) {
            this.ResetZoom();
        }

        private void MainPage_KeyDown(CoreWindow sender, KeyEventArgs args) {
            if (args.VirtualKey == Windows.System.VirtualKey.Control) {
                this.MousewheelInterceptBorder.IsHitTestVisible = false;
            }
        }

        private void MainPage_KeyUp(CoreWindow sender, KeyEventArgs args) {
            if (args.VirtualKey == Windows.System.VirtualKey.Control) {
                this.MousewheelInterceptBorder.IsHitTestVisible = true;
            }
        }

        /* we need to handle mouse events differently to get panning on drag. */
        private Point? dragStart;
        private Point? offsetsAtDragStart;
        private bool actuallyDragged;

        private void ImageContainer_PointerPressed(object sender, PointerRoutedEventArgs e) {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse
                    || !(e.GetCurrentPoint(this.ImageContainer) is { } point)
                    || !point.Properties.IsLeftButtonPressed
                    ) {
                return;
            }

            this.dragStart = e.GetCurrentPoint(this.ImageContainer).Position;
            this.offsetsAtDragStart = new Point(this.ImageContainer.HorizontalOffset, this.ImageContainer.VerticalOffset);
        }

        private async void ImageContainer_PointerReleased(object sender, PointerRoutedEventArgs e) {
            /* remarks: once PointerPressed is triggered, any other pointers get routed to PointerMoved instead.
             * this means that if dragStart is not null, then we must have received the same pointer that triggered
             * PointerPressed, i.e. a mouse left button. */
            if (this.dragStart != null) {
                this.dragStart = null;
                this.offsetsAtDragStart = null;

                if (!this.actuallyDragged) {
                    await this.ViewModel.SeekAsync(this.ViewModel.CurrentImageIndex + 1);
                }

                this.actuallyDragged = false;
            }
        }

        private void ImageContainer_PointerMoved(object sender, PointerRoutedEventArgs e) {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) {
                return;
            }

            if (!(this.dragStart is { } dragStart)) {
                return;
            }

            if (!(this.offsetsAtDragStart is { } startingOffsets)) {
                throw new ProgrammerError("This should be set in MouseDown!");
            }

            var offsets = new Point(
                dragStart.X - e.GetCurrentPoint(this.ImageContainer).Position.X,
                dragStart.Y - e.GetCurrentPoint(this.ImageContainer).Position.Y
            );

            if (!this.actuallyDragged) {
                if (Math.Abs(offsets.X) < 5 && Math.Abs(offsets.Y) < 5) {
                    return;
                }
            }

            this.actuallyDragged = true;

            _ = this.ImageContainer.ChangeView(startingOffsets.X + offsets.X, startingOffsets.Y + offsets.Y, null, true);
        }

        #endregion

        /* reference: https://docs.microsoft.com/en-us/windows/uwp/design/shell/title-bar#full-customization-example */
        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args) {
            this.LeftPaddingColumn.Width = new GridLength(sender.SystemOverlayLeftInset);
            this.RightPaddingColumn.Width = new GridLength(sender.SystemOverlayRightInset);

            this.AppTitleBar.Height = sender.Height;
        }

        // copied from ComicItemGrid.xaml.cs
        private bool resizing;

        private void MainPage_ResizeStarted(CoreWindow sender, object args) {
            this.resizing = true;
        }

        private void MainPage_ResizeCompleted(CoreWindow sender, object args) {
            this.resizing = false;

            if (this.ViewModel.DecodeImageHeight == null) {
                return;
            }

            this.UpdateDecodeImageHeight();
        }

        private void ImageContainer_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (this.resizing || this.ViewModel.DecodeImageHeight == null) {
                return;
            }

            this.UpdateDecodeImageHeight();
        }

        private void UpdateDecodeImageHeight(double? overrideZoomFactor = null) {
            var zoomFactor = overrideZoomFactor ?? this.ImageContainer.ZoomFactor;
            var resolutionScale = (double)DisplayInformation.GetForCurrentView().ResolutionScale / 100;
            this.ViewModel.DecodeImageHeight = (int)(this.ImageContainer.ActualHeight * zoomFactor * resolutionScale);
        }
    }
}
