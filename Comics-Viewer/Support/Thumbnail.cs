using ComicsLibrary;
using ComicsViewer.Profiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

#nullable enable

namespace ComicsViewer.Thumbnails {
    public static class Thumbnail {
        // TODO this class in unused
        public static string ThumbnailPath(Comic comic) {
            return Path.Combine(Defaults.ThumbnailFolderPath, $"{comic.UniqueIdentifier}.thumbnail.jpg");
        }

        public static async Task GenerateThumbnailAsync(Comic comic) {
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

            using var thumbnail = await imageFile.GetThumbnailAsync(ThumbnailMode.SingleItem, 500);
            var buffer = new byte[thumbnail.Size];
            var readBuffer = await thumbnail.ReadAsync(buffer.AsBuffer(), (uint)buffer.Length, Windows.Storage.Streams.InputStreamOptions.None);

            var thumbnailFile = await thumbnailsFolder.CreateFileAsync($"{comic.UniqueIdentifier}.thumbnail.jpg");
            using var stream = await thumbnailFile.OpenStreamForWriteAsync();
            await stream.WriteAsync(readBuffer.ToArray(), 0, (int)readBuffer.Length);
            await stream.FlushAsync();
        }
    }

    public enum GenerateThumbnailStatus {
        Success, Failed, Existing
    }
}
