using System;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Support;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicSubitemContainer : ViewModelBase {
        public ComicSubitem Subitem { get; }
        public BitmapSource? ThumbnailImage { get; private set; }
        public string Title => this.Subitem.DisplayName;
        public string Subtitle => this.Subitem.Files.Count.PluralString("Item");

        public ComicSubitemContainer(ComicSubitem subitem) {
            this.Subitem = subitem;
        }

        public async Task InitializeAsync(int? decodePixelHeight = null) {
            // remember subitems are checked to have at least one file when they are created
            var firstFile = this.Subitem.Files.First();

            if (!FileTypes.IsImage(firstFile)) {
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(firstFile);
            var image = new BitmapImage { DecodePixelType = DecodePixelType.Logical };

            if (decodePixelHeight is { } h) {
                image.DecodePixelHeight = h;
            }

            await image.SetSourceAsync(await file.OpenReadAsync());

            this.ThumbnailImage = image;
            this.OnPropertyChanged(nameof(this.ThumbnailImage));
        }
    }
}
