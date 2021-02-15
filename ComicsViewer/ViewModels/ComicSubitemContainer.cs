using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
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
            if (this.Subitem.Files.FirstOrDefault() is not { } firstFile) {
                return;
            }

            if (!ImageExtensions.Contains(Path.GetExtension(firstFile))) {
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(firstFile);
            var image = new BitmapImage { DecodePixelType = DecodePixelType.Logical } ;

            if (decodePixelHeight is { } h) {
                image.DecodePixelHeight = h;
            }

            await image.SetSourceAsync(await file.OpenReadAsync());

            this.ThumbnailImage = image;
            this.OnPropertyChanged(nameof(this.ThumbnailImage));
        }

        private static readonly string[] ImageExtensions = {
            ".bmp", ".gif", ".heic", ".heif", ".j2k", ".jfi", ".jfif", ".jif", ".jp2", ".jpe", ".jpeg", ".jpf",
            ".jpg", ".jpm", ".jpx", ".mj2", ".png", ".tif", ".tiff", ".webp"
        };
    }
}
