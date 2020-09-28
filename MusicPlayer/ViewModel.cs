#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.Common;
using Windows.ApplicationModel.Core;
using Windows.Media.Core;
using Windows.Storage;
using Windows.UI.Text;

namespace MusicPlayer {
    public class ViewModel : ViewModelBase {
        public List<PlaylistItem> PlaylistItems { get; } = new List<PlaylistItem>();
        public string Title { get; private set; } = "Player";

        public ViewModel() {
            this.PlayFailed += this.OnPlayFailed;
        }

        public string Description => this.CurrentDescription ?? "";
        private string? _currentDescription;
        public string? CurrentDescription {
            get => this._currentDescription;
            set {
                if (this._currentDescription == value) {
                    return;
                }

                this._currentDescription = value;
                this.OnPropertyChanged(nameof(this.Description));
            }
        }

        private PlaylistItem? nowPlaying;

        public async Task OpenContainingFolderAsync(StorageFile item, bool append = false) {
            if (!(await item.GetParentAsync() is StorageFolder folder)) {
                await ExpectedExceptions.UnauthorizedAccessAsync(cancelled: false);
                return;
            }

            await this.OpenFolderAsync(folder, item.Name, append);
        }

        public async Task OpenFolderAsync(StorageFolder item, string? startAtName = null, bool append = false) {
            await this.OpenFilesAsync(await item.GetFilesAsync(), startAtName, append);
        }

        public async Task OpenFilesAsync(IEnumerable<StorageFile> items, string? startAtName = null, bool append = false) {
            if (!append) {
                this.PlaylistItems.Clear();
            }

            var newItems = new List<PlaylistItem>();

            var startIndex = 0;
            var currentIndex = 0;
            foreach (var file in items.Where(f => IsPlayableFile(f.Name))) {
                newItems.Add(await PlaylistItem.FromFileAsync(file));

                if (file.Name == startAtName) {
                    startIndex = currentIndex;
                }

                currentIndex += 1;
            }

            newItems.Sort((a, b) => NaturalOrder.Comparer.Compare(a.Name, b.Name));

            this.PlaylistItems.AddRange(newItems);

            if (!append && this.PlaylistItems.Count > 0) {
                await this.PlayAsync(this.PlaylistItems[startIndex]);
            }

            this.PlaylistChanged?.Invoke(this, this.PlaylistItems);
        }

        public async Task OpenFilesAtPathAsync(string[] filenames) {
            if (filenames.Length == 0) {
                return;
            }

            try {
                var firstFile = await StorageFile.GetFileFromPathAsync(filenames[0]);
                await this.OpenFilesAsync(new[] { firstFile });
            } catch (UnauthorizedAccessException) {
                await ExpectedExceptions.UnauthorizedAccessAsync();
                return;
            } catch (FileNotFoundException) {
                await ExpectedExceptions.FileNotFoundAsync(filenames[0]);
                return;
            }

            var subsequentFiles = new List<StorageFile>();

            try {
                for (var i = 1; i < filenames.Length; i++) {
                    subsequentFiles.Add(await StorageFile.GetFileFromPathAsync(filenames[i]));
                } 
            } catch (UnauthorizedAccessException) {
                await ExpectedExceptions.UnauthorizedAccessAsync();
                return;
            } catch (FileNotFoundException) {
                await ExpectedExceptions.FileNotFoundAsync(filenames[0]);
                return;
            }

            await this.OpenFilesAsync(subsequentFiles, append: true);
        }

        public async Task PlayAsync(PlaylistItem item) {
            try {
                var source = MediaSource.CreateFromStorageFile(item.File);
                this.nowPlaying = item;

                // sometimes this gets called from the wrong thread
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal, 
                    () => {
                        this.PlayRequested?.Invoke(this, source);
                        this.PlayStarted?.Invoke(this, item);
                        this.Title = $"Player - {item.Name}";
                        this.OnPropertyChanged(nameof(this.Title));
                    }
                );
            } catch (FormatException) {
                this.PlayFailed?.Invoke(this, item);
            }
        }

        public PlaylistItem? Next() {
            if (this.nowPlaying is null) {
                return null;
            }

            var index = this.PlaylistItems.IndexOf(this.nowPlaying) + 1;

            if (index >= this.PlaylistItems.Count) {
                return null;
            }

            return this.PlaylistItems[index];
        }

        private async void OnPlayFailed(ViewModel sender, PlaylistItem item) {
            var index = this.PlaylistItems.IndexOf(item);
            this.PlaylistItems.RemoveAt(index);

            if (index > this.PlaylistItems.Count) {
                index -= 1;
            }

            if (this.PlaylistItems.Count > 0) {
                await this.PlayAsync(this.PlaylistItems[index]);
            }
        }

        public event Action<ViewModel, IReadOnlyList<PlaylistItem>>? PlaylistChanged;
        public event Action<ViewModel, MediaSource>? PlayRequested;
        public event Action<ViewModel, PlaylistItem>? PlayStarted;
        public event Action<ViewModel, PlaylistItem>? PlayFailed;

        private static readonly string[] PlayableFileTypes = new[] {
            ".mp3", ".wav", ".m4a", ".flac"
        };

        private static bool IsPlayableFile(string filename) {
            var ext = Path.GetExtension(filename).ToLowerInvariant();
            return PlayableFileTypes.Contains(ext);
        }
    }
}
