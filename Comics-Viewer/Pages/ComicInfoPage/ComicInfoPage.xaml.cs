using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is ComicInfoPageNavigationArguments args)) {
                throw new ApplicationLogicException();
            }

            this.ContainerFlyout = args.ParentFlyout;
            this.ContainerFlyout.Closing += this.ContainerFlyout_Closing;

            this.ViewModel = new ComicInfoPageViewModel(args.ParentViewModel, args.ComicItem);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
            this.ContainerFlyout!.Closing -= this.ContainerFlyout_Closing;
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

        private void EditThumbnailButton_Click(object sender, RoutedEventArgs e) {
            throw new NotImplementedException();
        }

        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel!.SaveComicInfoAsync(
                title: this.ComicTitleTextBox.Text,
                author: this.ComicAuthorTextBox.Text,
                tags: this.ComicTagsTextBox.Text,
                loved: this.ComicLovedCheckBox.IsChecked ?? throw new ApplicationLogicException(),
                disliked: this.ComicDislikedCheckBox.IsChecked ?? throw new ApplicationLogicException()
            );

            this.RevertChangesButton.Visibility = Visibility.Collapsed;
            this.SaveChangesButton.Visibility = Visibility.Collapsed;
        }

        private void RevertChangesButton_Click(object sender, RoutedEventArgs e) {
            this.ViewModel!.RefreshComicInfo();

            this.RevertChangesButton.Visibility = Visibility.Collapsed;
            this.SaveChangesButton.Visibility = Visibility.Collapsed;
        }

        private FlyoutBase? ContainerFlyout { get; set; }
        private async void ContainerFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args) {
            if (this.RevertChangesButton.Visibility == Visibility.Visible) {
                var result = await this.UnsavedChangesContentDialog.ShowAsync();
                if (result == ContentDialogResult.Primary) {
                    await this.ViewModel!.SaveComicInfoAsync(
                        title: this.ComicTitleTextBox.Text,
                        author: this.ComicAuthorTextBox.Text,
                        tags: this.ComicTagsTextBox.Text,
                        loved: this.ComicLovedCheckBox.IsChecked ?? throw new ApplicationLogicException(),
                        disliked: this.ComicDislikedCheckBox.IsChecked ?? throw new ApplicationLogicException()
                    );
                }
            }
        }


        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            this.ContainerFlyout?.Hide();
        }

        private void ComicInfoElement_Edited(object sender, RoutedEventArgs e) {
            // tries to not do this when textboxes change after input is validated
            if (((Control)sender).FocusState == FocusState.Unfocused) {
                return;
            }

            this.RevertChangesButton.Visibility = Visibility.Visible;
            this.SaveChangesButton.Visibility = Visibility.Visible;
        }
    }
}
