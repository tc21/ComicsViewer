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

                this.selectedSortIndex = value;
                this.OnPropertyChanged();
            }
        }

        /* semi-manually managed properties */
        public readonly ObservableCollection<ComicItem> ComicItems = new ObservableCollection<ComicItem>();
        private IEnumerable<ComicItem>? comicItemSource;

        private void SetComicItems(IEnumerable<ComicItem> items) {
            this.ComicItems.Clear();
            this.comicItemSource = items;
            this.RequestComicItems();
        }

        public void RequestComicItems() {
            if (this.comicItemSource == null) {
                return;
            }

            this.ComicItems.AddRange(this.comicItemSource.Take(100));
            this.comicItemSource = this.comicItemSource.Skip(100);

            this.OnPropertyChanged(nameof(this.VisibleItemCount));
        }

        /* manually managed properties */
        public string[] SortSelectors => Sorting.SortSelectorNames;
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
            this.comics = comics.Sorted(this.GetWorkItemSortSelector((Sorting.SortSelector)this.SelectedSortIndex));

            // Note: please keep this line before setting SelectedSortIndex...
            this.PropertyChanged += this.ComicViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged += this.MainViewModel_ProfileChanged;
            this.comics.ComicChanged += this.Comics_ComicChanged;

            // Loads the actual comic items
            this.RefreshComicItems();
        }

        private SortedComicView.SortSelector GetWorkItemSortSelector(Sorting.SortSelector sortSelector) {
            return sortSelector switch {
                Sorting.SortSelector.Title => SortedComicView.SortSelector.Title,
                Sorting.SortSelector.Author => SortedComicView.SortSelector.Author,
                Sorting.SortSelector.DateAdded => SortedComicView.SortSelector.DateAdded,
                Sorting.SortSelector.ItemCount => SortedComicView.SortSelector.Author,
                Sorting.SortSelector.Random => SortedComicView.SortSelector.Random,
                _ => throw new ApplicationLogicException($"{nameof(GetWorkItemSortSelector)}: unhandled switch case"),
            };
        }

        ~ComicItemGridViewModel() {
            Debug.WriteLine($"VM{debug_this_count} destroyed");
        }

        public static ComicItemGridViewModel ForTopLevelNavigationTag(MainViewModel appViewModel) {
            if (appViewModel.ActiveNavigationTag == MainViewModel.SecondLevelNavigationTag) {
                throw new ApplicationLogicException($"ForTopLevelNavigationTag was called when navigationTag was {appViewModel.ActiveNavigationTag}");
            }

            return new ComicItemGridViewModel(appViewModel, appViewModel.ComicView);
        }

        public static ComicItemGridViewModel ForSecondLevelNavigationTag(MainViewModel appViewModel, IEnumerable<Comic> comics) {
            if (appViewModel.ActiveNavigationTag != MainViewModel.SecondLevelNavigationTag) {
                throw new ApplicationLogicException($"ForSecondLevelNavigationTag was called when navigationTag was {appViewModel.ActiveNavigationTag}");
            }

            var set = comics.Select(c => c.UniqueIdentifier).ToHashSet();
            return new ComicItemGridViewModel(appViewModel, appViewModel.ComicView.Filtered(c => set.Contains(c.UniqueIdentifier)));
        }

        internal void RefreshComicItems() {
            // TODO
            var comicItems = this.CreateSortedComicItems(this.comics.ToList(), (Sorting.SortSelector)this.SelectedSortIndex);

            this.SetComicItems(comicItems);
        }

        #region Filtering and grouping

        private  IEnumerable<ComicItem> CreateSortedComicItems(List<Comic> comics, Sorting.SortSelector sortSelector) {
            // although this method call can take a while, the program isn't in a useable state between the user choosing 
            // a new sort selector and the sort finishing anyway
            if (this.navigationTag == MainViewModel.DefaultNavigationTag || this.navigationTag == MainViewModel.SecondLevelNavigationTag) {
                return comics.Select(comic => {
                    var item = ComicItem.WorkItem(comic, trackChangesFrom: this.comics);
                    item.RequestingRefresh += this.ComicItem_RequestingRefresh;
                    return item;
                });
            }
        
            return Sorting.SortAndCreateComicItems(comics, sortSelector, this.navigationTag, this.comics, this.ComicItem_RequestingRefresh);
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
                        break;
                    default:
                        throw new ApplicationLogicException("Unhandled switch case");
                }
            }
        }

        #endregion

        #region Opening items 

        public async Task OpenItemsAsync(IEnumerable<ComicItem> items) {
            if (items.First().ItemType == ComicItemType.Navigation) {
                if (items.Count() != 1) {
                    throw new ApplicationLogicException("Should not allow the user to open multiple navigation" +
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
                throw new ApplicationLogicException("Custom thumbnails for groupped items is not supported.");
            }

            var cached = comicItem.TitleComic.Metadata.ThumbnailSource;
            comicItem.TitleComic.Metadata.ThumbnailSource = file.Path.GetPathRelativeTo(comicItem.TitleComic.Path);

            bool success;

            try {
                success = await Thumbnail.GenerateThumbnailFromStorageFileAsync(
                    comicItem.TitleComic, file, this.MainViewModel.Profile, replace: true);
            } catch (Exception e) {
                comicItem.TitleComic.Metadata.ThumbnailSource = cached;
                throw e;
            }


            if (success) {
                this.MainViewModel.NotifyThumbnailChanged(comicItem.TitleComic);
            } else {
                comicItem.TitleComic.Metadata.ThumbnailSource = cached;
            }
        }

        public async Task TryRedefineThumbnailFromFilePickerAsync(ComicItem comicItem) {
            if (comicItem.ItemType != ComicItemType.Work) {
                throw new ApplicationLogicException("Custom thumbnails for groupped items is not supported.");
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
                    this.comics.Sort(this.GetWorkItemSortSelector((Sorting.SortSelector)this.SelectedSortIndex));

                    break;
            }
        }

        private void Filter_FilterChanged(Filter filter) {
            this.RefreshComicItems();
        }

        private void MainViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            this.OnPropertyChanged(nameof(this.ImageHeight));
            this.OnPropertyChanged(nameof(this.ImageWidth));
            this.OnPropertyChanged(nameof(this.ProfileName));
        }

        private static int debug_count = 0;
        private readonly int debug_this_count = ++debug_count;

        private void Comics_ComicChanged(ComicView sender, ComicChangedEventArgs args) {
            Debug.WriteLine($"VM{debug_this_count} {nameof(Comics_ComicChanged)} called for view model {this.navigationTag}");

            switch (args.Type) {
                case ComicChangedType.Add:
                    /* If we are on a WorkItem tab, we just need to add the comics to the view.
                     * If we are on another tab, though, we will need to be able to create new ComicItems on the fly
                     * to handle new item groups, and we say fuck that */
                    if (!(this.navigationTag == MainViewModel.DefaultNavigationTag
                            || this.navigationTag == MainViewModel.SecondLevelNavigationTag)) {
                        this.RefreshComicItems();
                        break;
                    }

                    var newComicItems = args.Comics!.Select(comic => {
                        var item = ComicItem.WorkItem(comic, this.comics);
                        item.RequestingRefresh += this.ComicItem_RequestingRefresh;
                        return item;
                    });

                    foreach (var item in newComicItems) {
                        this.ComicItems.Insert(0, item);
                    }

                    /* Generate thumbnails for added items */
                    /* There may be many view models active at any given moment. The if statement ensures that only
                     * the top level grid (guaranteed to be unique) requests thumbnails to be generated */
                    if (this.navigationTag != MainViewModel.SecondLevelNavigationTag) {
                        this.RequestGenerateThumbnails(newComicItems);
                    }

                    break;

                case ComicChangedType.Modified:
                case ComicChangedType.Remove:
                    // TODO: When modifying a comic (not removing), we may remove a tag/category/author that 
                    // belongs to the currently active second-level navigation item. The item should be removed,
                    // but isn't due to navigation items not knowing what kind of navigation item they are. We
                    // reversed the previous behavior of navigating out due to it being bad UX but the current 
                    // behavior is technically incorrect.
                    if (!(this.navigationTag == MainViewModel.DefaultNavigationTag 
                            || this.navigationTag == MainViewModel.SecondLevelNavigationTag)) {
                        this.RefreshComicItems();
                    }

                    // individual ComicItems will call ComicItem_RequestingRefresh to update or remove themselves
                    break;
                case ComicChangedType.Refresh:
                    this.RefreshComicItems();
                    break;
                case ComicChangedType.ReloadThumbnail:
                    break;
                default:
                    throw new ApplicationLogicException($"{nameof(Comics_ComicChanged)}: unhandled switch case");
            }
        }

        public async Task ToggleDislikedStatusForComicsAsync(IEnumerable<ComicItem> selectedItems) {
            var comics = selectedItems.Select(item => item.TitleComic).ToList();
            var newStatus = !comics.All(item => item.Disliked);

            foreach (var item in selectedItems) {
                item.TitleComic.Metadata.Disliked = newStatus;
            }

            await this.MainViewModel.NotifyComicsChangedAsync(comics);
        }

        public async Task ToggleLovedStatusForComicsAsync(IEnumerable<ComicItem> selectedItems) {
            var comics = selectedItems.Select(item => item.TitleComic).ToList();
            var newStatus = !comics.All(item => item.Loved);

            foreach (var item in selectedItems) {
                item.TitleComic.Metadata.Loved = newStatus;
            }

            await this.MainViewModel.NotifyComicsChangedAsync(comics);
        }

        internal void Dispose() {
            this.PropertyChanged -= this.ComicViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged -= this.MainViewModel_ProfileChanged;
            this.comics.ComicChanged -= this.Comics_ComicChanged;

            foreach (var item in this.ComicItems) {
                item.RequestingRefresh -= this.ComicItem_RequestingRefresh;
            }

            this.ComicItems.Clear();
        }
    }
}
