using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable

namespace ImageViewer {
    public class ViewModel : INotifyPropertyChanged {
        public static readonly string[] ImageExtensions = {
            ".bmp", ".gif", ".heic", ".heif", ".j2k", ".jfi", ".jfif", ".jif", ".jp2", ".jpe", ".jpeg", ".jpf",
            ".jpg", ".jpm", ".jpx", ".mj2", ".png", ".tif", ".tiff", ".webp"
        };

        // if you set this directly, make sure all items are valid images
        internal readonly List<StorageFile> Images = new List<StorageFile>();

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
                    return this.Images[this.CurrentImageIndex].Path;
                }

                return null;
            }
        }

        public string Title {
            get {
                var title = "Viewer";

                if (this.Images.Count > 0) {
                    title += $" - {this.CurrentImageIndex + 1}/{this.Images.Count}: {Path.GetFileName(this.CurrentImagePath)}";
                }

                return title;
            }
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
        public async Task LoadImagesAsync(IEnumerable<StorageFile> files, int? seekTo = 0) {
            this.Images.Clear();
            this.Images.AddRange(files.Where(f => IsImage(f.Path)));

            if (seekTo is int index) {
                await this.SeekAsync(index, reload: true);
            }
        }

        public async Task SeekAsync(int index, bool reload = false) {
            if (this.Images.Count == 0) {
                this.SetCurrentImageIndex(0);
                await this.SetCurrentImageSourceAsync(null);
                return;
            }

            index = this.ActualIndex(index);

            if (!reload && index == this.CurrentImageIndex) {
                return;
            }

            this.SetCurrentImageIndex(index);

            if (!await this.UpdateBitmapSourceAsync(index)) {
                this.Images.RemoveAt(index);
                await this.SeekAsync(index, reload: true);
                return;
            }
        }

        public async Task OpenContainingFolderAsync(StorageFile file) {
            if (!(await file.GetParentAsync() is StorageFolder parent)) {
                /* this means that the user hasn't enabled broadFileSystemAccess. should we show a warning? */
                await this.LoadImagesAsync(new[] { file });
                return;
            }

            var files = await parent.GetFilesAsync();

            await this.LoadImagesAsync(files, seekTo: null);

            for (var i = 0; i < this.Images.Count; i++) {
                if (this.Images[i].Name == file.Name) {
                    await this.SeekAsync(i, reload: true);
                }
            }
        }

        // for a relative-to-current-image index, just do CurrentImageIndex += ...
        private int ActualIndex(int index) {
            return (index + this.Images.Count) % this.Images.Count;
        }

        private void SetCurrentImageIndex(int i) {
            this.CurrentImageIndex = i;
            this.OnPropertyChanged(nameof(this.CurrentImagePath));
            this.OnPropertyChanged(nameof(this.Title));
        }

        private async Task SetCurrentImageSourceAsync(StorageFile? file) {
            if (file == null) {
                this.CurrentImageSource = null;
                this.CurrentImageMetadata = null;
                return;
            }

            var image = new BitmapImage();

            using (var stream = await file.OpenReadAsync()) {
                await image.SetSourceAsync(stream);
            }

            this.CurrentImageSource = image;

            await this.UpdateBitmapMetadataAsync(file);
        }

        // note: index must be valid!
        private bool updatingBitmapSource;
        private int? queuedIndex;
        private async Task<bool> UpdateBitmapSourceAsync(int index) {
            if (this.updatingBitmapSource) {
                this.queuedIndex = index;
                return true;
            }

            this.updatingBitmapSource = true;

            var file = this.Images[index];

            bool result;

            try {
                await this.SetCurrentImageSourceAsync(file);
                result = true;
            } catch (NotSupportedException) {
                result = false;
            }

            this.updatingBitmapSource = false;

            if (this.queuedIndex is int i) {
                this.queuedIndex = null;
                _ = await this.UpdateBitmapSourceAsync(i);
                // we can't really notify that another image is invalid yet
                return true;
            } else {
                return result;
            }
        }

        public async Task DeleteCurrentImageAsync() {
            var file = this.Images[this.CurrentImageIndex];
            this.Images.RemoveAt(this.CurrentImageIndex);
            var reloadTask = this.SeekAsync(this.CurrentImageIndex, reload: true);
            await file.DeleteAsync();  // this moves the file to recycle bin, if possible
            await reloadTask;
        }

        private async Task UpdateBitmapMetadataAsync(StorageFile stream) {
            try {
                var properties = await stream.GetBasicPropertiesAsync();
                var imageProperties = await stream.Properties.GetImagePropertiesAsync();
                this.CurrentImageMetadata = this.CurrentImageDescription(properties);
            } catch (NotSupportedException) {
                this.CurrentImageMetadata = null;
            }
        }

        private string CurrentImageDescription(BasicProperties? basicProperties, ImageProperties? metadata = null) {
            if (this.CurrentImagePath == null || this.CurrentImageSource == null) {
                return "No images open.";
            }

            if (basicProperties == null) {
                return "No metadata loaded.";
            }

            var description = $"File size: {FormatFileSize(basicProperties.Size)}\nLast modified: {basicProperties.DateModified}";

            try {
                description += $"\nDimensions: {this.CurrentImageSource.PixelWidth}x{this.CurrentImageSource.PixelHeight}";
            } catch (NotSupportedException) {
                /* do nothing */
            }

            if (metadata != null) {
                try {
                    description += string.Format(
                        "\nMetadata: {0}, {1} {2}",
                        metadata.DateTaken, metadata.CameraManufacturer, metadata.CameraModel
                    );
                } catch (NotSupportedException) {
                    /* do nothing */
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
    

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}