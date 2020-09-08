using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ImageViewer {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page {
        private readonly ViewModel ViewModel = new ViewModel();

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
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is IEnumerable<StorageFile> files) {
                if (files.Count() == 1) {
                    await this.ViewModel.OpenContainingFolderAsync(files.First());
                } else {
                    await this.ViewModel.LoadImagesAsync(files);
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
            if (e.KeyModifiers == Windows.System.VirtualKeyModifiers.None) {
                if (e.GetCurrentPoint(this).Properties.MouseWheelDelta > 0) { // This means scrolled up
                    await this.ViewModel.SeekAsync(this.ViewModel.CurrentImageIndex - 1);
                } else {
                    await this.ViewModel.SeekAsync(this.ViewModel.CurrentImageIndex + 1);
                }

                e.Handled = true;
                return;
            }
        }

        #region Dropping

        private void Grid_DragOver(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Grid_Drop(object sender, DragEventArgs e) {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) {
                return;
            }

            var items = await e.DataView.GetStorageItemsAsync();

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

        public void ResetZoom() {
            // For some reason, you have to wait a while before calling ChangeView
            _ = new Timer(async _ => await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                _ = this.ImageContainer.ChangeView(0, 0, 1))
            , null, 10, Timeout.Infinite);
        }

        public void ZoomImage(double scale) {
            // For some reason, you have to wait a while before calling ChangeView
            _ = new Timer(async _ => await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                /* Although ChangeView automatically constrains zooms to MaxZoomFactor, we need the accurate value for our calculations below.
                 * We don't need to do this for MinZoomFactor, because the image is already forced to be centered in that case. */
                if (scale * this.ImageContainer.ZoomFactor > this.ImageContainer.MaxZoomFactor) {
                    scale = this.ImageContainer.MaxZoomFactor / this.ImageContainer.ZoomFactor;
                }

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
                    (float)(this.ImageContainer.ZoomFactor * scale)
                );
            }), null, 10, Timeout.Infinite);
        }

        private void ImageContainer_PointerWheelChanged(object sender, PointerRoutedEventArgs e) {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) {
                return;
            }

            // Scrolling with mouse wheel is just disabled
            this.Page_PointerWheelChanged(sender, e);
            e.Handled = true;
        }

        /* we need to handle mouse events differently to get panning on drag. */
        private void ImageContainer_PointerPressed(object sender, PointerRoutedEventArgs e) {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) {
                return;
            }
        }

        private void ImageContainer_PointerReleased(object sender, PointerRoutedEventArgs e) {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) {
                return;
            }
        }

        private void ImageContainer_PointerMoved(object sender, PointerRoutedEventArgs e) {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) {
                return;
            }
        }

        #endregion

        /* reference: https://docs.microsoft.com/en-us/windows/uwp/design/shell/title-bar#full-customization-example */
        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args) {
            this.LeftPaddingColumn.Width = new GridLength(sender.SystemOverlayLeftInset);
            this.RightPaddingColumn.Width = new GridLength(sender.SystemOverlayRightInset);

            this.AppTitleBar.Height = sender.Height;
        }
    }
}
