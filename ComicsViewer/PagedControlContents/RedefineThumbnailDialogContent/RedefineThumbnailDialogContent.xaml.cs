using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    /* For, I'm assuming, security reasons, UWP dosen't allow you to open an File Picker to an arbitrary location. 
     * The result of this decision, obviously, isn't that programs won't want to open the file picker to arbitrary
     * locations, but that we will have to write our own file pickers. */
    public sealed partial class RedefineThumbnailDialogContent : Page, IPagedControlContent {
        public PagedControlAccessor? PagedControlAccessor { get; private set; }
        public ComicItemGridViewModel? ParentViewModel { get; private set; }
        public ComicWorkItem? Item { get; private set; }

        public RedefineThumbnailDialogContent() {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            var (accessor, args) 
                = PagedControlAccessor.FromNavigationArguments<RedefineThumbnailDialogNavigationArguments>(e.Parameter);

            this.PagedControlAccessor = accessor;
            this.Item = args.Item;
            this.ParentViewModel = args.ParentViewModel;

            var items = new List<ThumbnailGridItem>();

            // Note: this loop takes up a lot of memory
            foreach (var file in args.Files) {
                var image = new BitmapImage();
                var stream = await file.GetScaledImageAsThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem);
                await image.SetSourceAsync(stream);
                items.Add(new ThumbnailGridItem(file, image));
            }

            this.ThumbnailGrid.ItemsSource = items;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e) {
            if (!(this.ThumbnailGrid.SelectedItem is ThumbnailGridItem item)) {
                throw new ProgrammerError();
            }

            await this.ParentViewModel!.TryRedefineThumbnailAsync(this.Item!, item.File);
            this.PagedControlAccessor!.CloseContainer();
        }

        private async void CustomFileButton_Click(object sender, RoutedEventArgs e) {
            await this.ParentViewModel!.TryRedefineThumbnailFromFilePickerAsync(this.Item!);
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
        public IEnumerable<StorageFile> Files { get; }
        public ComicWorkItem Item { get; }
        public ComicItemGridViewModel ParentViewModel { get; }

        public RedefineThumbnailDialogNavigationArguments(IEnumerable<StorageFile> files, ComicWorkItem item, ComicItemGridViewModel parentViewModel) {
            this.Files = files;
            this.Item = item;
            this.ParentViewModel = parentViewModel;
        }
    }
}
