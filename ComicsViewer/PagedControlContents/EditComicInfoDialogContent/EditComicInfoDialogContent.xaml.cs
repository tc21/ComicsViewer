using ComicsViewer.Features;
using ComicsViewer.Controls;
using ComicsViewer.ViewModels.Pages;
using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ComicsViewer.Common;
using ComicsViewer.Uwp.Common;

#nullable enable

namespace ComicsViewer.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EditComicInfoDialogContent : IPagedControlContent {
        public EditComicInfoDialogContent() {
            this.InitializeComponent();
        }

        private EditComicInfoDialogViewModel? _viewModel;
        private EditComicInfoDialogViewModel ViewModel => this._viewModel ?? throw new ProgrammerError("ViewModel must be initialized");

        public PagedControlAccessor? PagedControlAccessor { get; private set; }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            var (controller, args) = 
                PagedControlAccessor.FromNavigationArguments<EditComicInfoDialogNavigationArguments>(
                    e.Parameter ?? throw new ProgrammerError("e.Parameter must not be null"));
            this.PagedControlAccessor = controller;

            this._viewModel = new EditComicInfoDialogViewModel(args.MainViewModel, args.ComicItem);
        }

        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel.SaveComicInfoAsync(
                title: this.ComicTitleTextBox.Text,
                tags: this.ComicTagsTextBox.Text,
                loved: this.ComicLovedCheckBox.IsChecked ?? throw new ProgrammerError()
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

            if (!items[0].IsOfType(StorageItemTypes.File) || !UserProfile.IsImage(items[0].Name)) {
                _ = await new ContentDialog { Content = "Please select an image as the new thumbnail.", Title = "Invalid thumbnail file" }.ScheduleShowAsync();
                return;
            }

            await this.ViewModel.MainViewModel.TryRedefineComicThumbnailAsync(this.ViewModel.Item.Comic, (StorageFile)items[0]);
        }

        private void ThumbnailBorder_DragEnter(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }
}
