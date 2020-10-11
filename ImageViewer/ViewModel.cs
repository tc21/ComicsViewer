using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.Common;
using ComicsViewer.Uwp.Common;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable

namespace ImageViewer {
    public class ViewModel : ViewModelBase {
        private static readonly string[] ImageExtensions = {
            ".bmp", ".gif", ".heic", ".heif", ".j2k", ".jfi", ".jfif", ".jif", ".jp2", ".jpe", ".jpeg", ".jpf",
            ".jpg", ".jpm", ".jpx", ".mj2", ".png", ".tif", ".tiff", ".webp"
        };

        // load default values from Settings
        public ViewModel() {
            this.IsMetadataVisible = Settings.Get<bool>("IsMetadataVisible");
        }

        // if you set this directly, make sure all items are valid images
        internal readonly List<StorageFile> Images = new List<StorageFile>();

        public int CurrentImageIndex { get; private set; }

        private bool _isMetadataVisible;
        public bool IsMetadataVisible {
            get => this._isMetadataVisible;
            set {
                this._isMetadataVisible = value;
                Settings.Set("IsMetadataVisible", value);
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

        private string _title = "Viewer";
        public string Title {
            get => this._title;
            private set {
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
            private set {
                this._currentImageSource = value;
                this.OnPropertyChanged();
            }
        }

        private string? _currentImageMetadata;
        public string? CurrentImageMetadata {
            get => this._currentImageMetadata ?? "No metadata loaded.";
            private set {
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

        public async Task LoadImagesAsync(IEnumerable<StorageFile> files, int? seekTo = 0, bool append = false) {
            this.CanSeek = false;

            if (!append) {
                this.Images.Clear();
            }

            this.Images.AddRange(files.Where(f => IsImage(f.Path)));
            this.CanSeek = true;

            if (seekTo is { } index) {
                if (append) {
                    throw new ArgumentException("For the moment, you must not seek when appending");
                }

                await this.SeekAsync(index, reload: true);
            } else {
                this.UpdateTitle();
            }
        }

        public async Task LoadImagesAtPathsAsync(IEnumerable<string> paths) {
            paths = paths.ToList();

            if (!paths.Any()) {
                return;
            }

            var first = paths.First();
            var rest = paths.Skip(1);

            // we need to verify if we have permission
            if (!(await ExpectedExceptions.TryGetFileWithPermission(first) is { } firstFile)) {
                return;
            }

            await this.LoadImagesAsync(new[] { firstFile });

            foreach (var filename in rest) {
                // this will crash if any file in <paths> doesn't exist
                await this.LoadImagesAsync(new[] { await StorageFile.GetFileFromPathAsync(filename) }, seekTo: null, append: true);
            }
        }

        private async Task LoadViaPassthrough<T>(StorageFile passthrough, Func<Task<T>> getAllFiles) where T : IEnumerable<StorageFile> {
            this.CanSeek = false;
            this.Title = "Viewer - Loading related files...";
            var passthroughSuccessful = await this.UpdateBitmapSourceAsync(passthrough);

            var files = await getAllFiles();
            // will set CanSeek = true
            await this.LoadImagesAsync(files, seekTo: null);

            var passthroughIndex = this.Images.FindIndex(file => file.Name == passthrough.Name);
            if (passthroughSuccessful) {
                this.SetCurrentImageIndex(passthroughIndex);
            } else {
                await this.SeekAsync(passthroughIndex, reload: true);
            }
        }

        public async Task OpenContainingFolderAsync(StorageFile file) {
            if (!(await file.GetParentAsync() is { } parent)) {
                /* this means that the user hasn't enabled broadFileSystemAccess */
                await ExpectedExceptions.ShowDialogAsync(
                    title: "Access denied",
                    message: "Viewer could not access the parent folder of the opened file. "
                        + "This is required to view multiple files in a folder. "
                        + "This is likely because File system access hasn't been enabled for this app. "
                        + "Please check that it's enabled in Settings > Privacy > File system.",
                    cancelled: false
                );

                await this.LoadImagesAsync(new[] { file });
                return;
            }

            await this.LoadViaPassthrough(file, async () => await parent.GetFilesInNaturalOrderAsync());
        }

        public bool CanSeek { get; private set; }

        public async Task SeekAsync(int index, bool reload = false) {
            if (!this.CanSeek) {
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

        private async Task SetCurrentImageSourceAsync(StorageFile? file, int? decodePixelHeight) {
            if (file == null) {
                this.CurrentImageSource = null;
                this.CurrentImageMetadata = null;
                return;
            }

            var image = new BitmapImage();

            using (var stream = await file.OpenReadAsync()) {
                if (decodePixelHeight is { } height) {
                    image.DecodePixelHeight = height;
                }

                await image.SetSourceAsync(stream);
            }

            this.CurrentImageSource = image;

            await this.UpdateBitmapMetadataAsync(file);
        }

        // note: index must be valid!
        private bool updatingBitmapSource;
        private StorageFile? queuedFile;

        private async Task<bool> UpdateBitmapSourceAsync(StorageFile file) {
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
            }

            this.updatingBitmapSource = false;

            if (this.queuedFile is { } f) {
                this.queuedFile = null;
                _ = await this.UpdateBitmapSourceAsync(f);
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

        private async Task UpdateBitmapMetadataAsync(StorageFile file) {
            try {
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
                    description += "\nMetadata: Taken " + string.Join(' ', info);
                }
            }

            return description;
        }

        private static bool IsImage(string path) {
            return ImageExtensions.Contains(Path.GetExtension(path).ToLower());
        }

        private static readonly string[] FilesizeUnits = { "bytes", "KB", "MB", "GB" };
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