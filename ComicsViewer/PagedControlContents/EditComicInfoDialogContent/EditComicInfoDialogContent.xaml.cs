using System;
using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.Uwp.Common;
using ComicsViewer.ViewModels.Pages;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EditComicInfoDialogContent : IPagedControlContent<EditComicInfoDialogNavigationArguments> {
        public EditComicInfoDialogContent() {
            this.InitializeComponent();
        }

        private EditComicInfoDialogViewModel? _viewModel;
        private EditComicInfoDialogViewModel ViewModel => this._viewModel ?? throw ProgrammerError.Unwrapped();

        public PagedControlAccessor? PagedControlAccessor { get; private set; }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            var (controller, args) =
                PagedControlAccessor.FromNavigationArguments<EditComicInfoDialogNavigationArguments>(
                    e.Parameter ?? throw ProgrammerError.Unwrapped("e.Parameter"));
            this.PagedControlAccessor = controller;

            this._viewModel = new EditComicInfoDialogViewModel(args.MainViewModel, args.ComicItem);
        }

        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel.SaveComicInfoAsync(
                displayTitle: this.DisplayTitleTextBox.Text,
                title: this.TitleTextBox.Text,
                author: this.AuthorTextBox.Text,
                category: this.CategoryTextBox.Text,
                dateAdded: this.DateAddedTextBox.Text,
                tags: this.ComicTagsTextBox.Text,
                loved: this.ComicLovedCheckBox.IsChecked ?? throw ProgrammerError.Unwrapped()
            );

            this.PagedControlAccessor!.CloseContainer();
        }

        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            this.ViewModel.UpdateIntendedChanges(title: this.TitleTextBox.Text);
        }

        private void AuthorTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            this.ViewModel.UpdateIntendedChanges(author: this.AuthorTextBox.Text);
        }

        private void CategoryTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            this.ViewModel.UpdateIntendedChanges(category: this.CategoryTextBox.Text);
        }

        private void DateAddedTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            this.ViewModel.UpdateIntendedChanges(dateAdded: this.DateAddedTextBox.Text);
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
