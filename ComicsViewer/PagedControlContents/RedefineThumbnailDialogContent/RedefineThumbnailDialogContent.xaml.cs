using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    /* For, I'm assuming, security reasons, UWP dosen't allow you to open an File Picker to an arbitrary location. 
     * The result of this decision, obviously, isn't that programs won't want to open the file picker to arbitrary
     * locations, but that we will have to write our own file pickers. */
    public sealed partial class RedefineThumbnailDialogContent : IPagedControlContent<RedefineThumbnailDialogNavigationArguments> {
        private MainViewModel? _mainViewModel;
        private ComicWorkItem? _item;

        public PagedControlAccessor? PagedControlAccessor { get; private set; }
        private MainViewModel MainViewModel => this._mainViewModel ?? throw new ProgrammerError("ViewModel must be initialized");
        private ComicWorkItem Item => this._item ?? throw new ProgrammerError("Item must be initialized");

        public RedefineThumbnailDialogContent() {
            this.InitializeComponent();
        }

        private readonly ObservableCollection<ThumbnailGridItem> ThumbnailGridSource = new();

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            var (accessor, args) = PagedControlAccessor.FromNavigationArguments<RedefineThumbnailDialogNavigationArguments>(
                e.Parameter ?? throw new ProgrammerError("e.Parameter must not be null")
            );

            this.PagedControlAccessor = accessor;
            this._item = args.Item;
            this._mainViewModel = args.MainViewModel;

            // Note: this loop takes up a lot of memory
            await foreach (var file in Thumbnail.GetPossibleThumbnailFilesAsync(args.Path)) {
                var image = new BitmapImage();
                var stream = await file.GetScaledImageAsThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem);
                await image.SetSourceAsync(stream);
                this.ThumbnailGridSource.Add(new ThumbnailGridItem(file, image));
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e) {
            if (this.ThumbnailGrid.SelectedItem is not ThumbnailGridItem item) {
                throw new ProgrammerError();
            }

            await this.MainViewModel.TryRedefineComicThumbnailAsync(this.Item.Comic, item.File);
            this.PagedControlAccessor!.CloseContainer();
        }

        private async void CustomFileButton_Click(object sender, RoutedEventArgs e) {
            await this.MainViewModel.TryRedefineComicThumbnailFromFilePickerAsync(this.Item.Comic);
            this.PagedControlAccessor!.CloseContainer();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            this.PagedControlAccessor!.CloseContainer();
        }
    }

    public class ThumbnailGridItem {
        public StorageFile File { get; }
        public BitmapImage BitmapImage { get; }

        public ThumbnailGridItem(StorageFile file, BitmapImage bitmapImage) {
            this.File = file;
            this.BitmapImage = bitmapImage;
        }
    }

    public class RedefineThumbnailDialogNavigationArguments {
        public string Path { get; }
        public ComicWorkItem Item { get; }
        public MainViewModel MainViewModel { get; }

        public RedefineThumbnailDialogNavigationArguments(string files, ComicWorkItem item, MainViewModel mainViewModel) {
            this.Path = files;
            this.Item = item;
            this.MainViewModel = mainViewModel;
        }
    }
}
