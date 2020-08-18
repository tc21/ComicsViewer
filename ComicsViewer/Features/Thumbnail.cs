﻿using ComicsLibrary;
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
            if (!(await TryGetThumbnailSourceAsync(comic) is StorageFile imageFile)) {
                return false;
            }

            return await GenerateThumbnailFromStorageFileAsync(comic, imageFile, profile, replace);
        }

        public static async Task<bool> GenerateThumbnailFromStorageFileAsync(Comic comic, StorageFile file, UserProfile profile, bool replace = false) {
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

            var bitmap = await GetSoftwareBitmapAsync(file);

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

        public static async Task<SoftwareBitmap> GetSoftwareBitmapAsync(StorageFile file) {
            using var inStream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(inStream);
            return await decoder.GetSoftwareBitmapAsync();
        }

        private static async Task<StorageFile?> TryGetThumbnailSourceAsync(Comic comic) {
            if (comic.ThumbnailSource is string path) {
                try {
                    if (await StorageFile.GetFileFromPathAsync(path) is StorageFile file) {
                        return file;
                    }
                } catch (UnauthorizedAccessException) {
                    // weird shit happens with windows and file permissions. just ignore this
                } catch (FileNotFoundException) {
                    // we pretend the ThumbnailSource isn't set
                }
            }

            var comicFolder = await StorageFolder.GetFolderFromPathAsync(comic.Path);
            return await TryGetFirstValidThumbnailFileAsync(comicFolder);
        }

        private static async Task<StorageFile?> TryGetFirstValidThumbnailFileAsync(StorageFolder folder) {
            var files = await GetPossibleThumbnailFilesAsync(folder);
            if (files.Count() == 0) {
                return null;
            }

            return files.First();
        }

        public static async Task<IEnumerable<StorageFile>> GetPossibleThumbnailFilesAsync(StorageFolder folder) {
            var files = new List<StorageFile>();

            // we can allow for customization in the future
            var maxFiles = 5;
            foreach (var file in await folder.GetFilesAsync()) {
                if (IsValidThumbnailFile(file.Name)) {
                    files.Add(file);

                    if (--maxFiles <= 0) {
                        break;
                    }
                }
            }

            foreach (var subfolder in await folder.GetFoldersAsync()) {
                foreach (var file in await GetPossibleThumbnailFilesAsync(subfolder)) {
                    files.Add(file);
                }
            }

            return files;

            static bool IsValidThumbnailFile(string filename) {
                // Windows media player/groove music creates these files which we can see but can't (and don't want to) use.
                if (filename == "AlbumArtSmall.jpg" || filename == "Folder.jpg") {
                    return false;
                }

                return UserProfile.ImageFileExtensions.Any(ext => filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    public enum GenerateThumbnailStatus {
        Success, Failed, Existing
    }
}
