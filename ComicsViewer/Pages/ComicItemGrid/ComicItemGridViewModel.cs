using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.Uwp.Common;
using Windows.ApplicationModel.Core;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public abstract class ComicItemGridViewModel : ViewModelBase {
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
                    value = Defaults.SettingsAccessor.DefaultSortSelection(this.NavigationTag, this.NavigationPageType);
                }

                this._selectedSortIndex = value;
                this.OnPropertyChanged();
            }
        }

        /* semi-manually managed properties */
        public readonly ObservableCollection<ComicItem> ComicItems = new();

        private protected virtual void SetComicItems(IEnumerable<ComicItem> items) {
            this.ComicItems.Clear();
            this.ComicItems.AddRange(items);
            this.OnPropertyChanged(nameof(this.TotalItemCount));
        }

        /* manually managed properties */
        public abstract string[] SortSelectors { get; }
        public int ImageHeight => this.MainViewModel.Profile.ImageHeight;
        public int ImageWidth => this.MainViewModel.Profile.ImageWidth;
        public int HighlightImageHeight => this.MainViewModel.Profile.ImageHeight / 2;
        public int HighlightImageWidth => this.MainViewModel.Profile.ImageWidth / 2;
        public string ProfileName => this.MainViewModel.Profile.Name;
        public int TotalItemCount => this.ComicItems.Count;
        internal readonly MainViewModel MainViewModel;

        // used for saving state
        internal double? RequestedInitialScrollOffset { get; set; }

        // Due to page caching, MainViewModel.ActiveNavigationTag might change throughout my lifecycle
        // Note: this is actually the page that contains this grid/viewmodel, not a parent page or anything. 
        // Think of a better name.
        internal readonly IMainPageContent parent;
        internal NavigationTag NavigationTag => this.parent.NavigationTag;
        internal NavigationPageType NavigationPageType => this.parent.NavigationPageType;

        /* pageType is used to remember the last sort by selection for each type of 
         * page (navigation tabs + details page) or to behave differently when navigating to different types of pages. 
         * It's not pretty but it's a very tiny part of the program. */
        private protected ComicItemGridViewModel(IMainPageContent parent, MainViewModel mainViewModel) {
            this.MainViewModel = mainViewModel;
            this.parent = parent;

            this.SelectedSortIndex = Defaults.SettingsAccessor.GetLastSortSelection(this.NavigationTag, this.NavigationPageType);

            // Note: we use this event handler to handle sorting, so that the above SelectedSortIndex assignment doesn't 
            // cause viewmodels to issue a sort before they're even properly initialized.
            this.PropertyChanged += this.ComicItemGridViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged += this.MainViewModel_ProfileChanged;

            // We won't call SortOrderChanged or anything here, so view models are expected to initialize themselves already sorted.
        }

        public static ComicItemGridViewModel ForTopLevelNavigationTag(IMainPageContent parent, MainViewModel mainViewModel, ComicItemGridState? savedState = null) {
            if (parent.NavigationTag.IsWorkItemNavigationTag()) {
                return new ComicWorkItemGridViewModel(parent, mainViewModel, mainViewModel.ComicView, savedState: savedState);
            } else {
                var initialSort = (ComicCollectionSortSelector)Defaults.SettingsAccessor.GetLastSortSelection(parent.NavigationTag, parent.NavigationPageType);

                return ComicNavigationItemGridViewModel.ForViewModel(parent, mainViewModel, parent.NavigationTag, initialSort);
            }
        }

        public static ComicWorkItemGridViewModel ForSecondLevelNavigationTag(
            IMainPageContent parent,
            MainViewModel appViewModel,
            ComicView comics,
            ComicItemGridViewModelProperties? properties,
            ComicItemGridState? savedState = null
        ) {
            return new ComicWorkItemGridViewModel(parent, appViewModel, comics, properties, savedState);
        }

        #region Filtering and grouping

        /* although these method calls can take a while, the program isn't in a useable state between the user choosing 
         * a new sort selector and the sort finishing anyway */
        public abstract void SortAndRefreshComicItems();

        #endregion

        #region Thumbnails 

        private readonly ConcurrentQueue<(Comic comic, bool replace)> thumbnailQueue = new();

        private readonly object thumbnailTaskStarting = new();

        public void ScheduleGenerateThumbnails(IEnumerable<Comic> comics, bool replace = false) {
            foreach (var comic in comics) {
                this.thumbnailQueue.Enqueue((comic, replace));
            }

            // This may not be thread safe
            lock (thumbnailTaskStarting) {
                if (!thumbnailQueue.IsEmpty && !this.MainViewModel.Tasks.Any(task => task.Name == "thumbnail")) {
                    this.StartProcessThumbnailQueueTask();
                }
            }
        }

        private void StartProcessThumbnailQueueTask() {
            _ = this.MainViewModel.StartUniqueTaskAsync(
                "thumbnail", $"Generating thumbnails...",
                (cc, p) => this.GenerateAndApplyThumbnailsInBackgroundThreadAsync(cc, p),
                // We call this again, to rerun the task if we didn't successfully clear the queue
                asyncCallback: () => { this.ScheduleGenerateThumbnails(Array.Empty<Comic>()); return Task.CompletedTask; },
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
            ;
        }

        private async Task GenerateAndApplyThumbnailsInBackgroundThreadAsync(CancellationToken cc, IProgress<int> progress) {
            var i = 0;

            while (!this.thumbnailQueue.IsEmpty) {
                if (cc.IsCancellationRequested) {
                    return;
                }

                if (!this.thumbnailQueue.TryDequeue(out var entry)) {
                    break;
                }

                var success = await Thumbnail.GenerateThumbnailAsync(entry.comic, this.MainViewModel.Profile, entry.replace);

                if (success) {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () => this.MainViewModel.NotifyThumbnailChanged(entry.comic)
                    );
                }

                progress.Report(i++);
            }
        }

        #endregion

        private void ComicItemGridViewModel_PropertyChanged(object _, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(this.SelectedSortIndex):
                    Defaults.SettingsAccessor.SetLastSortSelection(this.NavigationTag, this.NavigationPageType, this.SelectedSortIndex);
                    this.SortAndRefreshComicItems();

                    break;
            }
        }

        private void MainViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            this.OnPropertyChanged(nameof(this.ImageHeight));
            this.OnPropertyChanged(nameof(this.ImageWidth));
            this.OnPropertyChanged(nameof(this.HighlightImageHeight));
            this.OnPropertyChanged(nameof(this.HighlightImageWidth));
            this.OnPropertyChanged(nameof(this.ProfileName));
        }

        public virtual void RemoveEventHandlers() {
            this.PropertyChanged -= this.ComicItemGridViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged -= this.MainViewModel_ProfileChanged;
        }
    }
}
