using ComicsViewer.Features;
using ComicsViewer.Controls;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EditComicInfoDialogContent : Page, IPagedControlContent {
        public EditComicInfoDialogContent() {
            this.InitializeComponent();
        }

        public EditComicInfoDialogViewModel? ViewModel;
        public PagedControlAccessor? PagedControlAccessor { get; private set; }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            var (controller, args) = 
                PagedControlAccessor.FromNavigationArguments<EditComicInfoDialogNavigationArguments>(e.Parameter);
            this.PagedControlAccessor = controller;

            this.ViewModel = new EditComicInfoDialogViewModel(args.ParentViewModel, args.ComicItem);
        }

        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel!.SaveComicInfoAsync(
                title: this.ComicTitleTextBox.Text,
                tags: this.ComicTagsTextBox.Text,
                loved: this.ComicLovedCheckBox.IsChecked ?? throw new ProgrammerError(),
                disliked: this.ComicDislikedCheckBox.IsChecked ?? throw new ProgrammerError()
            );

            this.PagedControlAccessor!.CloseContainer();
        }

        private void DiscardChangesButton_Click(object sender, RoutedEventArgs e) {
            this.PagedControlAccessor!.CloseContainer();
        }

        private async void ThumbnailBorder_Drop(object sender, DragEventArgs e) {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) {
                return;
            }

            var items = await e.DataView.GetStorageItemsAsync();

            if (items.Count == 0) {
                return;
            }

            if (!items[0].IsOfType(StorageItemTypes.File) ||
                    !UserProfile.ImageFileExtensions.Any(ext => items[0].Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) {
                _ = await new ContentDialog { Content = "Please select an image as the new thumbnail.", Title = "Invalid thumbnail file" }.ShowAsync();
                return;
            }

            await this.ViewModel!.ParentViewModel.TryRedefineThumbnailAsync(this.ViewModel!.Item, (StorageFile)items[0]);
        }

        private void ThumbnailBorder_DragEnter(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }
}
