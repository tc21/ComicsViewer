using ComicsLibrary;
using ComicsViewer.Support;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Features;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public abstract class ComicItemGridViewModel : ViewModelBase, IDisposable {
        /* automatically managed properties */
        private int selectedSortIndex;
        public int SelectedSortIndex {
            get => this.selectedSortIndex;
            set {
                if (this.selectedSortIndex == value) {
                    return;
                }

                // turns out if for unforseen reasons this was set to an invalid value, it would crash the app
                if (value >= this.SortSelectors.Length) {
                    value = Defaults.SettingsAccessor.DefaultSortSelection(this.NavigationTag);
                }

                this.selectedSortIndex = value;
                this.OnPropertyChanged();
            }
        }

        /* semi-manually managed properties */
        public readonly ObservableCollection<ComicItem> ComicItems = new ObservableCollection<ComicItem>();
        private IEnumerator<ComicItem>? comicItemSource;

        private protected void SetComicItems(IEnumerable<ComicItem> items) {
            this.ComicItems.Clear();
            this.comicItemSource = items.GetEnumerator();
            this.RequestComicItems();
        }

        public void RequestComicItems() {
            if (this.comicItemSource == null) {
                return;
            }

            for (var i = 0; i < 100; i++) {
                if (this.comicItemSource.MoveNext()) {
                    this.ComicItems.Add(this.comicItemSource.Current);
                } else {
                    this.comicItemSource = null;
                    break;
                }
            }

            this.OnPropertyChanged(nameof(this.VisibleItemCount));
        }

        /* manually managed properties */
        public abstract string[] SortSelectors { get; }
        public int ImageHeight => this.MainViewModel.Profile.ImageHeight;
        public int ImageWidth => this.MainViewModel.Profile.ImageWidth;
        public string ProfileName => this.MainViewModel.Profile.Name;
        public int VisibleItemCount => this.ComicItems.Count;
        internal readonly MainViewModel MainViewModel;

        // Due to page caching, MainViewModel.ActiveNavigationTag might change throughout my lifecycle
        internal readonly NavigationTag NavigationTag;

        // for debug purposes
        private protected static int debug_count = 0;
        private protected readonly int debug_this_count = ++debug_count;

        /* pageType is used to remember the last sort by selection for each type of 
         * page (navigation tabs + details page) or to behave differently when navigating to different types of pages. 
         * It's not pretty but it's a very tiny part of the program. */
        private protected ComicItemGridViewModel(MainViewModel appViewModel) {
            Debug.WriteLine($"VM{debug_this_count} created");

            this.MainViewModel = appViewModel;
            this.NavigationTag = appViewModel.ActiveNavigationTag;

            this.SelectedSortIndex = Defaults.SettingsAccessor.GetLastSortSelection(this.MainViewModel.ActiveNavigationTag);

            // Note: we use this event handler to handle sorting, so that the above SelectedSortIndex assignment doesn't 
            // cause viewmodels to issue a sort before they're even properly initialized.
            this.PropertyChanged += this.ComicItemGridViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged += this.MainViewModel_ProfileChanged;

            // We won't call SortOrderChanged or anything here, so view models are expected to initialize themselves already sorted.
        }

        ~ComicItemGridViewModel() {
            Debug.WriteLine($"VM{debug_this_count} destroyed");
        }

        public static ComicItemGridViewModel ForTopLevelNavigationTag(MainViewModel appViewModel) {
            if (appViewModel.ActiveNavigationTag == NavigationTag.Detail) {
                throw new ProgrammerError($"ForTopLevelNavigationTag was called when navigationTag was {appViewModel.ActiveNavigationTag}");
            }

            if (appViewModel.ActiveNavigationTag.IsWorkItemNavigationTag()) {
                return new ComicWorkItemGridViewModel(appViewModel, appViewModel.Comics);
            } else {
                return new ComicNavigationItemGridViewModel(appViewModel, appViewModel.Comics);
            }
        }

        public static ComicWorkItemGridViewModel ForSecondLevelNavigationTag(MainViewModel appViewModel, ComicView comics) {
            if (appViewModel.ActiveNavigationTag != NavigationTag.Detail) {
                throw new ProgrammerError($"ForSecondLevelNavigationTag was called when navigationTag was {appViewModel.ActiveNavigationTag}");
            }

            return new ComicWorkItemGridViewModel(appViewModel, comics);
        }

        #region Filtering and grouping

        /* although these method calls can take a while, the program isn't in a useable state between the user choosing 
         * a new sort selector and the sort finishing anyway */
        private protected abstract void SortOrderChanged();

        #endregion

        #region Thumbnails 

        private protected async Task GenerateAndApplyThumbnailsInBackgroundThreadAsync(
                IEnumerable<ComicWorkItem> comicItems, bool replace, CancellationToken cc, IProgress<int> progress) {
            var i = 0;
            foreach (var item in comicItems) {
                var success = await Thumbnail.GenerateThumbnailAsync(item.Comic, this.MainViewModel.Profile, replace);
                if (success) {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () => this.MainViewModel.NotifyThumbnailChanged(item.Comic)
                    );
                }

                if (cc.IsCancellationRequested) {
                    return;
                }

                progress.Report(i++);
            }
        }

        public async Task TryRedefineThumbnailAsync(ComicWorkItem comicItem, StorageFile file) {
            var comic = comicItem.Comic.WithUpdatedMetadata(metadata => {
                metadata.ThumbnailSource = file.Path.GetPathRelativeTo(comicItem.Comic.Path);
                return metadata;
            });

            var success = await Thumbnail.GenerateThumbnailFromStorageFileAsync(comic, file, this.MainViewModel.Profile, replace: true);
            if (success) {
                this.MainViewModel.NotifyThumbnailChanged(comic);
                await this.MainViewModel.UpdateComicAsync(new[] { comic });
            }
        }

        public async Task TryRedefineThumbnailFromFilePickerAsync(ComicWorkItem comicItem) {
            var picker = new FileOpenPicker {
                ViewMode = PickerViewMode.Thumbnail
            };

            foreach (var extension in UserProfile.ImageFileExtensions) {
                picker.FileTypeFilter.Add(extension);
            }

            var file = await picker.PickSingleFileAsync();

            if (file == null) {
                return;
            }

            await this.TryRedefineThumbnailAsync(comicItem, file);
        }

        #endregion

        private void ComicItemGridViewModel_PropertyChanged(object _, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(this.SelectedSortIndex):
                    Defaults.SettingsAccessor.SetLastSortSelection(this.NavigationTag, this.SelectedSortIndex);
                    this.SortOrderChanged();

                    break;
            }
        }

        private void MainViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            this.OnPropertyChanged(nameof(this.ImageHeight));
            this.OnPropertyChanged(nameof(this.ImageWidth));
            this.OnPropertyChanged(nameof(this.ProfileName));
        }

        // Unlinks event handlers
        public virtual void Dispose() {
            this.MainViewModel.ProfileChanged -= this.MainViewModel_ProfileChanged;

            foreach (var item in this.ComicItems) {
                item.Dispose();
            }

            this.ComicItems.Clear();
        }
    }
}
