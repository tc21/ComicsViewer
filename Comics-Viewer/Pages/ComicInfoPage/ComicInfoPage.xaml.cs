using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public sealed partial class ComicInfoPage : Page {
        public ComicInfoPage() {
            this.InitializeComponent();
        }

        private ComicInfoPageViewModel? ViewModel;
        private FlyoutBase? ContainerFlyout { get; set; }
        private Action? EditInfoCallback { get; set; }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is ComicInfoPageNavigationArguments args)) {
                throw new ApplicationLogicException();
            }

            this.ContainerFlyout = args.ParentFlyout;
            this.EditInfoCallback = args.EditInfoCallback;
            this.ViewModel = new ComicInfoPageViewModel(args.ParentViewModel, args.ComicItem);
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            try {
                await this.ViewModel!.Initialize();
            } catch (UnauthorizedAccessException) {
                _ = await new MessageDialog("Please enable file system access in settings to open comics.", "Access denied").ShowAsync();
            }
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e) {
            if (!(e.ClickedItem is ComicSubitem item)) {
                throw new ApplicationLogicException();
            }

            await this.ViewModel!.OpenItem(item);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            this.ContainerFlyout?.Hide();
        }

        private void EditInfoButton_Click(object sender, RoutedEventArgs e) {
            this.ContainerFlyout?.Hide();
            this.EditInfoCallback?.Invoke();
        }
    }
}
