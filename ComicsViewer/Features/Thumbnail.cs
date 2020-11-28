﻿using ComicsLibrary;
using ComicsViewer.Uwp.Common;
using ComicsViewer.Uwp.Common.Win32Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Features {
    public static class Thumbnail {

        /* Current feature set:     
         *  Thumbnails are generated when a comic is added to the database AND a thumbnail doesn't already exist.
         *  The user can (re-)generate thumbnails for already added items using the context menu. 
         *  If an import is cancelled, thumbnail generation is cancelled. Already-generated thumbnails are not
         *  deleted, since thumbnails are designed to be "temp files". (TODO: actually make them temporary...) */

        public static string ThumbnailPath(Comic comic) {
            return Path.Combine(Defaults.ApplicationDataFolder.Path, "Thumbnails", $"{comic.UniqueIdentifier}.thumbnail.jpg");
        }

        public static async Task<bool> GenerateThumbnailAsync(Comic comic, UserProfile profile, bool replace = false) {
            if (!(await TryGetThumbnailSourceAsync(comic) is { } imageFile)) {
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

        private static async Task<SoftwareBitmap> GetSoftwareBitmapAsync(StorageFile file) {
            using var inStream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(inStream);
            return await decoder.GetSoftwareBitmapAsync();
        }

        private static async Task<StorageFile?> TryGetThumbnailSourceAsync(Comic comic) {
            if (comic.ThumbnailSource is { } path) {
                try {
                    return await StorageFile.GetFileFromPathAsync(path);
                } catch (UnauthorizedAccessException) {
                    // weird shit happens with windows and file permissions. just ignore this
                } catch (FileNotFoundException) {
                    // we pretend the ThumbnailSource isn't set
                }
            }

            return await TryGetFirstValidThumbnailFileAsync(comic.Path);
        }

        private static async Task<StorageFile?> TryGetFirstValidThumbnailFileAsync(string folder) {
            return await GetPossibleThumbnailFilesAsync(folder).FirstOrDefaultAsync();
        }

        public static async IAsyncEnumerable<StorageFile> GetPossibleThumbnailFilesAsync(string folder) {
            var files = IO.GetDirectoryContents(folder);
            var subfolders = new List<string>();
            var maxFiles = 5;

            foreach (var file in files) {
                if (file.ItemType == IO.FileOrDirectoryType.FileOrLink && IsValidThumbnailFile(file.Name) && maxFiles > 0) {
                    maxFiles -= 1;
                    yield return await StorageFile.GetFileFromPathAsync(file.Path);
                }

                if (file.ItemType == IO.FileOrDirectoryType.Directory) {
                    subfolders.Add(file.Path);
                }
            }

            foreach (var subfolder in subfolders) {
                await foreach (var file in GetPossibleThumbnailFilesAsync(subfolder)) {
                    yield return file;
                }
            }

            static bool IsValidThumbnailFile(string filename) {
                // Windows media player/groove music creates these files which we can see but can't (and don't want to) use.
                if (filename is "AlbumArtSmall.jpg" or "Folder.jpg") {
                    return false;
                }

                return UserProfile.IsImage(filename);
            }
        }
    }
}
