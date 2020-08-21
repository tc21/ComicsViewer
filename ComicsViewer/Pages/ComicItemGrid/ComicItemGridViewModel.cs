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
    public class ComicItemGridViewModel : ViewModelBase {
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
                    value = Defaults.SettingsAccessor.DefaultSortSelection(this.navigationTag);
                }

                this.selectedSortIndex = value;
                this.OnPropertyChanged();
            }
        }

        /* semi-manually managed properties */
        public readonly ObservableCollection<ComicItem> ComicItems = new ObservableCollection<ComicItem>();
        private IEnumerator<ComicItem>? comicItemSource;

        private void SetComicItems(IEnumerable<ComicItem> items) {
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
        public string[] SortSelectors => IsWorkItemNavigationTag(this.navigationTag)
            ? SortSelectorNames.ComicSortSelectorNames
            : SortSelectorNames.ComicPropertySortSelectorNames;

        public int ImageHeight => this.MainViewModel.Profile.ImageHeight;
        public int ImageWidth => this.MainViewModel.Profile.ImageWidth;
        public string ProfileName => this.MainViewModel.Profile.Name;
        public int VisibleItemCount => this.ComicItems.Count;

        internal readonly MainViewModel MainViewModel;
        
        private readonly SortedComicView comics;

        // Due to page caching, MainViewModel.ActiveNavigationTag might change throughout my lifecycle
        private readonly string navigationTag;
        // To preserve random sort order when filtering the underlying list of comics, we will need to manually keep
        // track of that order here
        //private Dictionary<string, int>? randomSortSelectors;

        /* pageType is used to remember the last sort by selection for each type of 
         * page (navigation tabs + details page) or to behave differently when navigating to different types of pages. 
         * It's not pretty but it's a very tiny part of the program. */
        private ComicItemGridViewModel(MainViewModel appViewModel, ComicView comics) {
            Debug.WriteLine($"VM{debug_this_count} created");

            this.MainViewModel = appViewModel;
            this.navigationTag = appViewModel.ActiveNavigationTag;

            this.SelectedSortIndex = Defaults.SettingsAccessor.GetLastSortSelection(this.MainViewModel.ActiveNavigationTag);

            // this sort result is instantly thrown away, but we don't need to optimize for that
            this.comics = comics.Sorted(ComicSortSelector.Random);

            // Note: please keep this line before setting SelectedSortIndex...
            this.PropertyChanged += this.ComicViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged += this.MainViewModel_ProfileChanged;
            this.comics.ComicsChanged += this.Comics_ComicsChanged;

            // Sorts and loads the actual comic items
            this.RefreshComicItems();
        }

        ~ComicItemGridViewModel() {
            Debug.WriteLine($"VM{debug_this_count} destroyed");
        }

        public static ComicItemGridViewModel ForTopLevelNavigationTag(MainViewModel appViewModel) {
            if (appViewModel.ActiveNavigationTag == MainViewModel.SecondLevelNavigationTag) {
                throw new ProgrammerError($"ForTopLevelNavigationTag was called when navigationTag was {appViewModel.ActiveNavigationTag}");
            }

            return new ComicItemGridViewModel(appViewModel, appViewModel.ComicView);
        }

        public static ComicItemGridViewModel ForSecondLevelNavigationTag(MainViewModel appViewModel, ComicView comics) {
            if (appViewModel.ActiveNavigationTag != MainViewModel.SecondLevelNavigationTag) {
                throw new ProgrammerError($"ForSecondLevelNavigationTag was called when navigationTag was {appViewModel.ActiveNavigationTag}");
            }

            return new ComicItemGridViewModel(appViewModel, comics);
        }

        #region Filtering and grouping

        private static bool IsWorkItemNavigationTag(string navigationTag) {
            return (navigationTag == MainViewModel.DefaultNavigationTag || navigationTag == MainViewModel.SecondLevelNavigationTag);
        }

        /* although these method calls can take a while, the program isn't in a useable state between the user choosing 
         * a new sort selector and the sort finishing anyway */
        private void RefreshComicItems() {
            if (IsWorkItemNavigationTag(this.navigationTag)) {
                this.SetComicWorkItems();
            } else {
                this.SortAndSetComicNavigationItems((ComicPropertySortSelector)this.SelectedSortIndex);
            }

        }

        private void SetComicWorkItems() {
            var items = this.comics.Select(comic => {
                var item = ComicItem.WorkItem(comic, trackChangesFrom: this.comics);
                item.RequestingRefresh += this.ComicItem_RequestingRefresh;
                return item;
            });

            this.SetComicItems(items);
        }

        /* We have an unfortunate discrepancy here, caused by how we implemented sorting:
         * You are supposed to call SortedComicView.Sort, which will then trigger events that call SetComicItems. So a
         * list of workItems is already sorted here. On the other hand, we have to manually sort our ComicPropertiesView,
         * because we didn't need to waste time working out an event-based ComicPropertiesView */
        private void SortAndSetComicNavigationItems(ComicPropertySortSelector sortSelector) { 
            var view = comics.SortedProperties(
                this.navigationTag switch {
                    "authors" => comic => new[] { comic.DisplayAuthor },
                    "categories" => comic => new[] { comic.DisplayCategory },
                    "tags" => comic => comic.Tags,
                    _ => throw new ProgrammerError("unhandled switch case")
                },
                sortSelector
            );

            var items = view.Select(property => {
                var item = ComicItem.NavigationItem(property.Name, property.Comics, view.PropertyView(property.Name));
                item.RequestingRefresh += this.ComicItem_RequestingRefresh;
                return item;
            });

            this.SetComicItems(items);
        }

        private void ComicItem_RequestingRefresh(ComicItem sender, ComicItem.RequestingRefreshType type) {
            if (this.ComicItems.Contains(sender)) {
                switch (type) {
                    case ComicItem.RequestingRefreshType.Reload:
                        var index = this.ComicItems.IndexOf(sender);
                        this.ComicItems.RemoveAt(index);
                        this.ComicItems.Insert(index, sender);
                        break;
                    case ComicItem.RequestingRefreshType.Remove:
                        sender.RequestingRefresh -= this.ComicItem_RequestingRefresh;
                        _ = this.ComicItems.Remove(sender);

                        if (this.ComicItems.Count < 100) {
                            this.RequestComicItems();
                        }

                        if (this.ComicItems.Count == 0 && this.navigationTag == MainViewModel.SecondLevelNavigationTag) {
                            this.MainViewModel.NavigateOut();
                        }

                        break;
                    default:
                        throw new ProgrammerError("Unhandled switch case");
                }
            }
        }

        #endregion

        #region Opening items 

        public async Task OpenItemsAsync(IEnumerable<ComicItem> items) {
            if (items.First().ItemType == ComicItemType.Navigation) {
                if (items.Count() != 1) {
                    throw new ProgrammerError("Should not allow the user to open multiple navigation" +
                                                        " items at once (use the search into feature instead)");
                }

                this.MainViewModel.NavigateInto(items.First());
                return;
            }

            // Only work items remain at this point
            // Although we don't have to await these, we will need to do so for it to throw an 
            // UnauthorizedAccessException when broadFileSystemAccess isn't enabled.
            try {
                var tasks = items.Select(item => Startup.OpenComicAtPathAsync(item.TitleComic.Path, this.MainViewModel.Profile));
                await Task.WhenAll(tasks);
            } catch (UnauthorizedAccessException) {
                await ExpectedExceptions.UnauthorizedFileSystemAccessAsync();
            } catch (FileNotFoundException e) {
                await ExpectedExceptions.FileNotFoundAsync(e.FileName);
            }
        }

        #endregion

        #region Thumbnails 

        public void RequestGenerateThumbnails(IEnumerable<ComicItem> comicItems, bool replace = false) {
            var copy = comicItems.ToList();
            _ = this.MainViewModel.StartUniqueTaskAsync(
                "thumbnail", $"Generating thumbnails for {copy.Count} items...",
                (cc, p) => this.GenerateAndApplyThumbnailsInBackgroundThreadAsync(copy, replace, cc, p),
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        private async Task GenerateAndApplyThumbnailsInBackgroundThreadAsync(
                IEnumerable<ComicItem> comicItems, bool replace, CancellationToken cc, IProgress<int> progress) {
            var i = 0;
            foreach (var item in comicItems) {
                var success = await Thumbnail.GenerateThumbnailAsync(item.TitleComic, this.MainViewModel.Profile, replace);
                if (success) {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () => this.MainViewModel.NotifyThumbnailChanged(item.TitleComic)
                    );
                }

                if (cc.IsCancellationRequested) {
                    return;
                }

                progress.Report(i++);
            }
        }

        public async Task TryRedefineThumbnailAsync(ComicItem comicItem, StorageFile file) {
            if (comicItem.ItemType != ComicItemType.Work) {
                throw new ProgrammerError("Custom thumbnails for groupped items is not supported.");
            }

            var comic = comicItem.TitleComic.WithUpdatedMetadata(metadata => {
                metadata.ThumbnailSource = file.Path.GetPathRelativeTo(comicItem.TitleComic.Path);
                return metadata;
            });

            var success = await Thumbnail.GenerateThumbnailFromStorageFileAsync(comic, file, this.MainViewModel.Profile, replace: true);
            if (success) {
                this.MainViewModel.NotifyThumbnailChanged(comic);
                await this.MainViewModel.UpdateComicAsync(new[] { comic });
            }
        }

        public async Task TryRedefineThumbnailFromFilePickerAsync(ComicItem comicItem) {
            if (comicItem.ItemType != ComicItemType.Work) {
                throw new ProgrammerError("Custom thumbnails for groupped items is not supported.");
            }

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

        /* Instead of putting logic in each observable property's setter, we put them here, to keep setter code the
         * same for each property */
        private void ComicViewModel_PropertyChanged(object _, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(this.SelectedSortIndex):
                    Defaults.SettingsAccessor.SetLastSortSelection(this.navigationTag, this.SelectedSortIndex);

                    if (IsWorkItemNavigationTag(this.navigationTag)) {
                        this.comics.Sort((ComicSortSelector)this.SelectedSortIndex);
                    }

                    this.RefreshComicItems();

                    break;
            }
        }

        private void MainViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            this.OnPropertyChanged(nameof(this.ImageHeight));
            this.OnPropertyChanged(nameof(this.ImageWidth));
            this.OnPropertyChanged(nameof(this.ProfileName));
        }

        private static int debug_count = 0;
        private readonly int debug_this_count = ++debug_count;

        private void Comics_ComicsChanged(ComicView sender, ComicsChangedEventArgs e) {
            Debug.WriteLine($"VM{debug_this_count} {nameof(Comics_ComicsChanged)} called for view model {this.navigationTag}");

            switch (e.Type) { // switch ChangeType
                case ComicChangeType.ItemsChanged:
                    /* Notes on adding: 
                     * If we are on a WorkItem tab, we just need to add the comics to the view.
                     * If we are on another tab, though, we will need to be able to create new ComicItems on the fly
                     * to handle new item groups, and we say fuck that */

                    /* notes on modifying: 
                     * TODO: When modifying a comic (not removing), we may remove a tag/category/author that 
                     * belongs to the currently active second-level navigation item. The item should be removed,
                     * but isn't due to navigation items not knowing what kind of navigation item they are. We
                     * reversed the previous behavior of navigating out due to it being bad UX but the current 
                     * behavior is technically incorrect. */

                    if (!IsWorkItemNavigationTag(this.navigationTag)) {
                        this.RefreshComicItems();
                        break;
                    }

                    var addedItems = e.Added.Select(comic => {
                        var item = ComicItem.WorkItem(comic, this.comics);
                        item.RequestingRefresh += this.ComicItem_RequestingRefresh;
                        this.ComicItems.Insert(0, item);
                        return item;
                    });

                    /* Generate thumbnails for added items */
                    /* There may be many view models active at any given moment. The if statement ensures that only
                     * the top level grid (guaranteed to be unique) requests thumbnails to be generated */
                    if (this.navigationTag != MainViewModel.SecondLevelNavigationTag) {
                        this.RequestGenerateThumbnails(addedItems);
                    }

                    /* individual ComicItems will call ComicItem_RequestingRefresh to update or remove themselves, 
                     * so we don't need any logic in this section to handle modified and removed. */
                    break;

                case ComicChangeType.Refresh:
                    this.RefreshComicItems();
                    break;

                case ComicChangeType.ThumbnailChanged:
                    break;

                default:
                    throw new ProgrammerError($"{nameof(Comics_ComicsChanged)}: unhandled switch case");
            }
        }

        public async Task ToggleDislikedStatusForComicsAsync(IEnumerable<ComicItem> selectedItems) {
            var newStatus = !comics.All(item => item.Disliked);

            var changes = selectedItems.Select(item => item.TitleComic.WithUpdatedMetadata(metadata => {
                metadata.Disliked = newStatus;
                return metadata;
            }));

            await this.MainViewModel.UpdateComicAsync(changes);
        }

        public async Task ToggleLovedStatusForComicsAsync(IEnumerable<ComicItem> selectedItems) {
            var comics = selectedItems.Select(item => item.TitleComic).ToList();
            var newStatus = !comics.All(item => item.Loved);

            var changes = selectedItems.Select(item => item.TitleComic.WithUpdatedMetadata(metadata => {
                metadata.Loved = newStatus;
                return metadata;
            }));

            await this.MainViewModel.UpdateComicAsync(changes);
        }

        internal void Dispose() {
            this.PropertyChanged -= this.ComicViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged -= this.MainViewModel_ProfileChanged;
            this.comics.ComicsChanged -= this.Comics_ComicsChanged;

            foreach (var item in this.ComicItems) {
                item.RequestingRefresh -= this.ComicItem_RequestingRefresh;
            }

            this.ComicItems.Clear();
        }
    }
}
