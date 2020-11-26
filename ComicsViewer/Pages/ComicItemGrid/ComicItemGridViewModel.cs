﻿using ComicsViewer.Support;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Features;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using ComicsLibrary.Collections;
using ComicsViewer.Common;
using ComicsLibrary.Sorting;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public abstract class ComicItemGridViewModel : ViewModelBase, IDisposable {
        /* automatically managed properties */
        private int _selectedSortIndex;
        public int SelectedSortIndex {
            get => this._selectedSortIndex;
            set {
                if (this._selectedSortIndex == value) {
                    return;
                }

                // turns out if for unforseen reasons this was set to an invalid value, it would crash the app
                if (value >= this.SortSelectors.Length) {
                    value = Defaults.SettingsAccessor.DefaultSortSelection(this.NavigationTag);
                }

                this._selectedSortIndex = value;
                this.OnPropertyChanged();
            }
        }

        /* semi-manually managed properties */
        public readonly ObservableCollection<ComicItem> ComicItems = new ObservableCollection<ComicItem>();
        private IEnumerator<ComicItem>? comicItemSource;

        private protected void SetComicItems(IEnumerable<ComicItem> items, int itemCount) {
            this.ComicItems.Clear();
            this.comicItemSource = items.GetEnumerator();
            this.RequestComicItems();

            this.TotalItemCount = itemCount;
            this.OnPropertyChanged(nameof(this.TotalItemCount));
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
        public int TotalItemCount { get; private set; }
        internal readonly MainViewModel MainViewModel;

        // Due to page caching, MainViewModel.ActiveNavigationTag might change throughout my lifecycle
        internal readonly NavigationTag NavigationTag;

        /* pageType is used to remember the last sort by selection for each type of 
         * page (navigation tabs + details page) or to behave differently when navigating to different types of pages. 
         * It's not pretty but it's a very tiny part of the program. */
        private protected ComicItemGridViewModel(MainViewModel appViewModel) {
            this.MainViewModel = appViewModel;
            this.NavigationTag = appViewModel.ActiveNavigationTag;

            this.SelectedSortIndex = Defaults.SettingsAccessor.GetLastSortSelection(this.MainViewModel.ActiveNavigationTag);

            // Note: we use this event handler to handle sorting, so that the above SelectedSortIndex assignment doesn't 
            // cause viewmodels to issue a sort before they're even properly initialized.
            this.PropertyChanged += this.ComicItemGridViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged += this.MainViewModel_ProfileChanged;

            // We won't call SortOrderChanged or anything here, so view models are expected to initialize themselves already sorted.
        }

        public static ComicItemGridViewModel ForTopLevelNavigationTag(MainViewModel appViewModel) {
            if (appViewModel.ActiveNavigationTag == NavigationTag.Detail) {
                throw new ProgrammerError($"ForTopLevelNavigationTag was called when navigationTag was {appViewModel.ActiveNavigationTag}");
            }

            if (appViewModel.ActiveNavigationTag.IsWorkItemNavigationTag()) {
                return new ComicWorkItemGridViewModel(appViewModel, appViewModel.ComicView);
            } else if (appViewModel.ActiveNavigationTag == NavigationTag.Playlist) {
                return ComicNavigationItemGridViewModel.ForViewModel(appViewModel, appViewModel.Playlists);
            } else {
                return ComicNavigationItemGridViewModel.ForViewModel(appViewModel,  // TODO check ComicCollectionSortSelector
                    GetSortedProperties(appViewModel.ComicView, appViewModel.ActiveNavigationTag, ComicCollectionSortSelector.Random));
            }
        }

        private static ComicPropertiesCollectionView GetSortedProperties(ComicView comics, NavigationTag navigationTag, ComicCollectionSortSelector sortSelector) {
            return comics.SortedProperties(
                navigationTag switch {
                    NavigationTag.Author => comic => new[] { comic.Author },
                    NavigationTag.Category => comic => new[] { comic.Category },
                    NavigationTag.Tags => comic => comic.Tags,
                    _ => throw new ProgrammerError("unhandled switch case")
                },
                sortSelector
            );
        }

        public static ComicWorkItemGridViewModel ForSecondLevelNavigationTag(
            MainViewModel appViewModel, 
            ComicView comics, 
            ComicItemGridViewModelProperties? properties
        ) {
            if (appViewModel.ActiveNavigationTag != NavigationTag.Detail) {
                throw new ProgrammerError($"ForSecondLevelNavigationTag was called when navigationTag was {appViewModel.ActiveNavigationTag}");
            }

            return new ComicWorkItemGridViewModel(appViewModel, comics, properties);
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
            var comic = comicItem.Comic.WithMetadata(thumbnailSource: file.RelativeTo(comicItem.Comic.Path));

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
