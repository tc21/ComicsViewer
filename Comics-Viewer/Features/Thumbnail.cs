using ComicsLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable

namespace ComicsViewer.Features {
    public static class Thumbnail {

        /* Current feature set:     
         *  Thumbnails are generated when a comic is added to the database AND a thumbnail doesn't already exist.
         *  The user can (re-)generate thumbnails for already added items using the context menu. 
         *  If an import is cancelled, thumbnail generation is cancelled. Already-generated thumbnails are not
         *  deleted, since thumbnails are designed to be "temp files". (TODO: actually make them temporary...) */

        public static string ThumbnailPath(Comic comic) {
            return Path.Combine(Defaults.ThumbnailFolderPath, $"{comic.UniqueIdentifier}.thumbnail.jpg");
        }

        public static async Task<bool> GenerateThumbnailAsync(Comic comic, UserProfile profile, bool replace = false) {
            var thumbnailsFolder = await Defaults.GetThumbnailFolderAsync();

            StorageFile? existingFile = null;

            try {
                existingFile = await thumbnailsFolder.GetFileAsync($"{comic.UniqueIdentifier}.thumbnail.jpg");
                if (!replace) {
                    return false;
                }
            } catch (FileNotFoundException) {
                // pass
            }

            if (!(await TryGetThumbnailSourceAsync(comic, profile) is StorageFile imageFile)) {
                return false;
            }

            using var inStream = await imageFile.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(inStream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();

            if (existingFile != null) {
                await existingFile.DeleteAsync();
            }

            var thumbnailFile = await thumbnailsFolder.CreateFileAsync($"{comic.UniqueIdentifier}.thumbnail.jpg");
            using var outStream = await thumbnailFile.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outStream);

            encoder.SetSoftwareBitmap(bitmap);
            encoder.BitmapTransform.ScaledWidth = (uint)(2 * profile.ImageWidth);
            encoder.BitmapTransform.ScaledHeight = (uint)(bitmap.PixelHeight * 2 * profile.ImageWidth / bitmap.PixelWidth);
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

            await encoder.FlushAsync();

            return true;
        }

        private static async Task<StorageFile?> TryGetThumbnailSourceAsync(Comic comic, UserProfile profile) {
            if (comic.ThumbnailSource is string path) {
                try {
                    if (await StorageFile.GetFileFromPathAsync(path) is StorageFile file) {
                        return file;
                    }
                } catch (FileNotFoundException) {
                    // TODO we should tell the comic to erase its ThumbnailSource value
                    // but otherwise do nothing
                } catch (UnauthorizedAccessException) {
                    // weird shit happens with windows and file permissions. just ignore this
                }
            }

            try {
                var comicFolder = await StorageFolder.GetFolderFromPathAsync(comic.Path);
                return await TryGetFirstFileMatchingExtensionAsync(comicFolder, UserProfile.ImageFileExtensions);
            } catch (FileNotFoundException) {
                Debug.WriteLine("TryGetThumbnailSourceAsync threw FileNotFoundException");
                // TODO we should throw the exception but catch it outside
            }

            return null;
        }

        private static async Task<StorageFile?> TryGetFirstFileMatchingExtensionAsync(
                StorageFolder folder, IEnumerable<string> extensions) {            
            foreach (var file in await folder.GetFilesAsync()) {
                if (IsDesirableFile(file.Name)) {
                    return file;
                }
            }

            foreach (var subfolder in await folder.GetFoldersAsync()) {
                if (await TryGetFirstFileMatchingExtensionAsync(subfolder, extensions) is StorageFile file) {
                    return file;
                }
            }

            return null;

            bool IsDesirableFile(string filename) {
                // Windows media player/groove music creates these files which we can see but can't (and don't want to) use.
                if (filename == "AlbumArtSmall.jpg" || filename == "Folder.jpg") {
                    return false;
                }

                return extensions.Any(ext => filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            }
        }


    }

    public enum GenerateThumbnailStatus {
        Success, Failed, Existing
    }
}
