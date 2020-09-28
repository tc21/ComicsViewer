using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ComicsViewer.Common;
using ComicsViewer.Uwp.Common.Win32Interop;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable

namespace ImageViewer {
    public class ViewModel : ViewModelBase {
        public static readonly string[] ImageExtensions = {
            ".bmp", ".gif", ".heic", ".heif", ".j2k", ".jfi", ".jfif", ".jif", ".jp2", ".jpe", ".jpeg", ".jpf",
            ".jpg", ".jpm", ".jpx", ".mj2", ".png", ".tif", ".tiff", ".webp"
        };

        // if you set this directly, make sure all items are valid images
        internal readonly List<string> Images = new List<string>();

        public int CurrentImageIndex { get; private set; }

        private bool _isMetadataVisible;
        public bool IsMetadataVisible {
            get => this._isMetadataVisible;
            set {
                this._isMetadataVisible = value;
                this.OnPropertyChanged();
            }
        }

        public string? CurrentImagePath {
            get {
                if (this.CurrentImageIndex < this.Images.Count) {
                    return this.Images[this.CurrentImageIndex];
                }

                return null;
            }
        }

        private string _title = "Viewer";
        public string Title {
            get => this._title;
            set {
                this._title = value;
                this.OnPropertyChanged();
            }
        }

        private void UpdateTitle() {
            var title = "Viewer";

            if (this.Images.Count > 0) {
                var indicator = "";
                if (this.Images.Count > 1) {
                    indicator = $"{this.CurrentImageIndex + 1}/{this.Images.Count}: ";
                }
                title += $" - {indicator}{Path.GetFileName(this.CurrentImagePath)}";
            }

            this.Title = title;
        }

        private BitmapSource? _currentImageSource;
        public BitmapSource? CurrentImageSource {
            get => this._currentImageSource;
            set {
                this._currentImageSource = value;
                this.OnPropertyChanged();
            }
        }

        private string? _currentImageMetadata;
        public string? CurrentImageMetadata {
            get => this._currentImageMetadata ?? "No metadata loaded.";
            set {
                this._currentImageMetadata = value;
                this.OnPropertyChanged();
            }
        }

        private int? _decodeImageHeight;
        public int? DecodeImageHeight {
            get => this._decodeImageHeight;
            set {
                if (this._decodeImageHeight == value) {
                    return;
                }

                this._decodeImageHeight = value;
                if (this.CurrentImageSource != null) {
                    _ = this.SetCurrentImageSourceAsync(this.Images[this.CurrentImageIndex], value).ConfigureAwait(false);
                }
            }
        }

        public async Task LoadImagesAsync(IEnumerable<string> files, int? seekTo = 0, bool append = false) {
            this.canSeek = false;

            if (!append) {
                this.Images.Clear();
            }

            this.Images.AddRange(files.Where(IsImage));
            this.canSeek = true;

            if (seekTo is int index) {
                if (append) {
                    throw new ArgumentException("For the moment, you must not seek when appending");
                }

                await this.SeekAsync(index, reload: true);
            } else {
                this.UpdateTitle();
            }
        }

        public async Task OpenContainingFolderAsync(StorageFile file) {
            if (!(await file.GetParentAsync() is StorageFolder parent)) {
                /* this means that the user hasn't enabled broadFileSystemAccess. should we show a warning? */
                await this.LoadImagesAsync(new[] { file.Path });
                return;
            }

            this.canSeek = false;
            this.Title = "Viewer - Loading related files...";
            var passthroughSuccessful = await this.UpdateBitmapSourceAsync(file.Path);
            var files = await parent.GetFilesAsync();

            await this.LoadImagesAsync(files.Select(f => f.Path), seekTo: null);

            for (var i = 0; i < this.Images.Count; i++) {
                if (this.Images[i] == file.Path) {
                    if (passthroughSuccessful) {
                        this.SetCurrentImageIndex(i);
                    } else {
                        await this.SeekAsync(i, reload: true);
                    }
                }
            }
        }

        public bool canSeek;

        public async Task SeekAsync(int index, bool reload = false) {
            if (!this.canSeek) {
                return;
            }

            if (this.Images.Count == 0) {
                this.SetCurrentImageIndex(0);
                await this.SetCurrentImageSourceAsync(null, this.DecodeImageHeight);
                return;
            }

            index = this.ActualIndex(index);

            if (!reload && index == this.CurrentImageIndex) {
                return;
            }

            this.SetCurrentImageIndex(index);

            if (!await this.UpdateBitmapSourceAsync(this.Images[index])) {
                this.Images.RemoveAt(index);
                await this.SeekAsync(index, reload: true);
                return;
            }
        }

        // for a relative-to-current-image index, just do CurrentImageIndex += ...
        private int ActualIndex(int index) {
            return (index + this.Images.Count) % this.Images.Count;
        }

        private void SetCurrentImageIndex(int i) {
            this.CurrentImageIndex = i;
            this.OnPropertyChanged(nameof(this.CurrentImagePath));
            this.UpdateTitle();
        }

        private async Task SetCurrentImageSourceAsync(string? file, int? decodePixelHeight) {
            if (file == null) {
                this.CurrentImageSource = null;
                this.CurrentImageMetadata = null;
                return;
            }

            var image = new BitmapImage();

            if (decodePixelHeight is int height) {
                image.DecodePixelHeight = height;
            }

            var success = false;

            while (!success) {
                using var stream = IO.OpenFileForRead(file);

                try {
                    await image.SetSourceAsync(stream);
                    success = true;
#pragma warning disable CS0618 // Type or member is obsolete
                } catch (ExecutionEngineException) {
                    // sometimes weird things happen on the Interop boundary, we just need to retry.
                    // And the documentation says that the runtime no longer raises this exception, but Win32 still does.
                    // Maybe we should move this into InteropReadStream, but I'm not quite sure where this is actually being thrown.
#pragma warning restore CS0618 // Type or member is obsolete
                    continue;
                }
            }

            this.CurrentImageSource = image;

            await this.UpdateBitmapMetadataAsync(file);
        }

        // note: index must be valid!
        private bool updatingBitmapSource;
        private string? queuedFile;

        private async Task<bool> UpdateBitmapSourceAsync(string file) {
            if (this.updatingBitmapSource) {
                this.queuedFile = file;
                return true;
            }

            this.updatingBitmapSource = true;

            bool result;

            try {
                await this.SetCurrentImageSourceAsync(file, this.DecodeImageHeight);
                result = true;
            } catch (NotSupportedException) {
                result = false;
            } catch (FileNotFoundException) {
                result = false;
            } finally {
                this.updatingBitmapSource = false;
            }


            if (this.queuedFile is string f) {
                this.queuedFile = null;
                _ = await this.UpdateBitmapSourceAsync(f);
                // we can't really notify that another image is invalid yet
                return true;
            } else {
                return result;
            }
        }

        public async Task DeleteCurrentImageAsync() {
            var file = await StorageFile.GetFileFromPathAsync(this.Images[this.CurrentImageIndex]);
            this.Images.RemoveAt(this.CurrentImageIndex);
            var reloadTask = this.SeekAsync(this.CurrentImageIndex, reload: true);
            await file.DeleteAsync();  // this moves the file to recycle bin, if possible
            await reloadTask;
        }

        private async Task UpdateBitmapMetadataAsync(string file_) {
            try {
                var file = await StorageFile.GetFileFromPathAsync(file_);
                var properties = await file.GetBasicPropertiesAsync();
                var imageProperties = await file.Properties.GetImagePropertiesAsync();
                this.CurrentImageMetadata = this.CurrentImageDescription(properties, imageProperties);
            } catch (NotSupportedException) {
                this.CurrentImageMetadata = null;
            }
        }

        private string CurrentImageDescription(BasicProperties? basicProperties, ImageProperties? metadata = null) {
            if (this.CurrentImageSource == null) {
                return "No images open.";
            }

            if (basicProperties == null) {
                return "No metadata loaded.";
            }

            var description = $"File size: {FormatFileSize(basicProperties.Size)}\nLast modified: {FormatDate(basicProperties.DateModified, true)}";

            try {
                description += $"\nDimensions: {this.CurrentImageSource.PixelWidth}x{this.CurrentImageSource.PixelHeight}";
            } catch (NotSupportedException) {
                /* do nothing */
            }

            if (metadata != null) {
                var info = new List<string>();

                if (metadata.DateTaken.Year > 1600) {
                    info.Add($"on {FormatDate(metadata.DateTaken)}");
                }

                if (!(string.IsNullOrEmpty(metadata.CameraManufacturer) && string.IsNullOrEmpty(metadata.CameraModel))) {
                    info.Add($"with {metadata.CameraManufacturer} {metadata.CameraModel}");
                }

                if (info.Any()) {
                    description += $"\nMetadata: Taken " + string.Join(' ', info);
                }
            }

            return description;
        }

        private static bool IsImage(string path) {
            return ImageExtensions.Contains(Path.GetExtension(path).ToLower());
        }

        private static readonly string[] FilesizeUnits = new[] { "bytes", "KB", "MB", "GB" };
        private const int FilesizeBase = 1024;  // Windows does 1024-based counting, so we'll do the same.
        private static string FormatFileSize(double size) {
            foreach (var unit in FilesizeUnits) {
                if (size >= FilesizeBase) {
                    size /= 1024;
                } else {
                    return $"{FormatDouble(size)} {unit}";
                }
            }

            // assuming there aren't actually any tb-sized images
            return $"{FormatDouble(size)} TB";

            static string FormatDouble(double d) {
                return $"{d:F2}".TrimEnd('0').TrimEnd('.');
            }
        }

        private static string FormatDate(DateTimeOffset offset, bool includeTime = false) {
            var formatted = offset.ToString("yyyy-MM-dd");

            if (includeTime) {
                formatted += " " + offset.ToString("t");
            }

            return formatted;
        }
    }
}