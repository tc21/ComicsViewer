using ComicsLibrary;
using ComicsViewer.Profiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable

namespace ComicsViewer.Thumbnails {
    public static class Thumbnail {
        // TODO this class in unused
        public static string ThumbnailPath(Comic comic) {
            return Path.Combine(Defaults.ThumbnailFolderPath, $"{comic.UniqueIdentifier}.thumbnail.jpg");
        }

        public static async Task GenerateThumbnailAsync(Comic comic, int width = 512) {
            var thumbnailsFolder = await Defaults.GetThumbnailFolderAsync();
            try {
                var existingThumbnail = await thumbnailsFolder.GetFileAsync($"{comic.UniqueIdentifier}.thumbnail.jpg");
                return;
            } catch (FileNotFoundException) {
                // pass
            }

            var comicFolder = await StorageFolder.GetFolderFromPathAsync(comic.Path);
            var imageFile = await new UserProfile().GetFirstFileForComicFolderAsync(comicFolder);
            if (imageFile == null) {
                return;
            }

            using var inStream = await imageFile.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(inStream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();

            var thumbnailFile = await thumbnailsFolder.CreateFileAsync($"{comic.UniqueIdentifier}.thumbnail.jpg");
            using var outStream = await thumbnailFile.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outStream);

            encoder.SetSoftwareBitmap(bitmap);
            encoder.BitmapTransform.ScaledWidth = (uint)width;
            encoder.BitmapTransform.ScaledHeight = (uint)(bitmap.PixelHeight * width / bitmap.PixelWidth);
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

            await encoder.FlushAsync();
        }
    }

    public enum GenerateThumbnailStatus {
        Success, Failed, Existing
    }
}
