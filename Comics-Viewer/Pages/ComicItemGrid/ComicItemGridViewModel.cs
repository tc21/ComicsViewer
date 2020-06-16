using ComicsLibrary;
using ComicsViewer.Filters;
using ComicsViewer.Profiles;
using ComicsViewer.Support;
using ComicsViewer.Support.ClassExtensions;
using ComicsViewer.Thumbnails;
using ComicsViewer.ViewModels;
using Microsoft.Toolkit.Uwp.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace ComicsViewer {
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
        internal void SetComicItems(IEnumerable<ComicItem> items) {
            this.ComicItems.Clear();

            foreach (var item in items) {
                this.ComicItems.Add(item);
            }

            this.OnPropertyChanged(nameof(this.VisibleItemCount));
        }

        /* manually managed properties */
        public string[] SortSelectors => Sorting.SortSelectorNames;
        public int ImageHeight => this.MainViewModel.Profile.ImageHeight;
        public int ImageWidth => this.MainViewModel.Profile.ImageWidth;
        public string ProfileName => this.MainViewModel.Profile.Name;
        public int VisibleItemCount => this.ComicItems.Count;

        internal readonly MainViewModel MainViewModel;
        private readonly ComicList comics;
        // Due to page caching, MainViewModel.ActiveNavigationTag might change throughout my lifecycle
        private readonly string navigationTag;
        // To preserve random sort order when filtering the underlying list of comics, we will need to manually keep
        // track of that order here
        private Dictionary<string, int> randomSortSelectors;

        /* pageType is used to remember the last sort by selection for each type of 
         * page (navigation tabs + details page) or to behave differently when navigating to different types of pages. 
         * It's not pretty but it's a very tiny part of the program. */
        public ComicItemGridViewModel(MainViewModel appViewModel, IEnumerable<Comic> comics) {
            this.MainViewModel = appViewModel;
            this.comics = new ComicList(comics);
            this.navigationTag = appViewModel.ActiveNavigationTag;

            // Note: please keep this line before setting SelectedSortIndex...
            this.PropertyChanged += this.ComicViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged += this.MainViewModel_ProfileChanged;
            this.MainViewModel.Filter.FilterChanged += this.Filter_FilterChanged;
            this.MainViewModel.ComicsModified += this.MainViewModel_ComicsModified;

            var allComicItems = CreateComicItems(this.comics, null, this.navigationTag);
            this.randomSortSelectors = allComicItems.Select(item => item.Title).Distinct().ToDictionary(e => e, _ => 0);

            this.SelectedSortIndex = Defaults.SettingsAccessor.GetLastSortSelection(this.MainViewModel.ActiveNavigationTag);

            // Loads the actual comic items
            this.RefreshComicItems();
        }

        internal void RefreshComicItems() {
            var comicItems = CreateComicItems(this.comics, this.MainViewModel.Filter, this.navigationTag);

            if ((Sorting.SortSelector)this.SelectedSortIndex == Sorting.SortSelector.Random) {
                // GetValueOrDefault is used here since items may have been added. to this.comics and we want the application to keep working
                this.SetComicItems(comicItems.OrderBy(i => this.randomSortSelectors.GetValueOrDefault(i.Title, 0)).ToList());
            } else {
                this.SetComicItems(Sorting.Sorted(comicItems, (Sorting.SortSelector)this.SelectedSortIndex));
            }
        }

        #region Filtering and grouping

        private static IEnumerable<ComicItem> CreateComicItems(IEnumerable<Comic> comics, Filter? filter, string navigationTag) {
            return navigationTag switch {
                "default" => FilterAndGroupComicItems(comics, filter, null),
                "comics" => FilterAndGroupComicItems(comics, filter, null),
                "authors" => FilterAndGroupComicItems(comics, filter, comic => new[] { comic.DisplayAuthor }),
                "categories" => FilterAndGroupComicItems(comics, filter, comic => new[] { comic.DisplayCategory }),
                "tags" => FilterAndGroupComicItems(comics, filter, comic => comic.Tags),
                _ => throw new ApplicationLogicException($"Invalid navigation tag '{navigationTag}' when creating comic items."),
            };
        }

        private static IEnumerable<ComicItem> FilterAndGroupComicItems(IEnumerable<Comic> comics, Filter? filter, Func<Comic, IEnumerable<string>>? groupBy) {
            if (filter != null) {
                comics = comics.Where(filter.ShouldBeVisible);
            }

            var comicItems = new List<ComicItem>();

            if (groupBy == null) {
                return comics.Select(comic => ComicItem.WorkItem(comic));
            } else {
                return GroupByMultiple(comics, groupBy);
            }
        }

        private static IEnumerable<ComicItem> GroupByMultiple(IEnumerable<Comic> comics, Func<Comic, IEnumerable<string>> groupBy) {
            var dict = new Dictionary<string, List<Comic>>();

            foreach (var comic in comics) {
                foreach (var key in groupBy(comic)) {
                    if (!dict.ContainsKey(key)) {
                        dict[key] = new List<Comic>();
                    }

                    dict[key].Add(comic);
                }
            }

            foreach (var pair in dict) {
                yield return ComicItem.NavigationItem(pair.Key, pair.Value);
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
                _ = await new MessageDialog("Please enable file system access in settings to open comics.", "Access denied").ShowAsync();
            } catch (FileNotFoundException) {
                _ = await new MessageDialog("The application is not currently capable of handling this error.", "File not found").ShowAsync();
            }
        }

        #endregion

        #region Thumbnails 

        public void RequestGenerateThumbnails(IEnumerable<ComicItem> comicItems, bool replace = false) {
            var copy = comicItems.ToList();
            this.MainViewModel.StartUniqueTask("thumbnail", $"Generating thumbnails for {copy.Count} items...",
                (cc, p) => this.GenerateAndApplyThumbnailsInBackgroundThread(copy, replace, cc, p));
        }

        private async Task GenerateAndApplyThumbnailsInBackgroundThread(
                IEnumerable<ComicItem> comicItems, bool replace, CancellationToken cc, IProgress<int> progress) {
            var i = 0;
            foreach (var item in comicItems) {
                var success = await Thumbnail.GenerateThumbnailAsync(item.TitleComic, this.MainViewModel.Profile, replace);
                if (success) {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () => item.DoNotifyThumbnailChanged());
                }

                if (cc.IsCancellationRequested) {
                    return;
                }

                progress.Report(i++);
            }
        }

        public async Task TryRedefineThumbnailAsync(ComicItem comicItem, string path) {
            if (comicItem.ItemType != ComicItemType.Work) {
                throw new ApplicationLogicException("Custom thumbnails for groupped items is not supported.");
            }

            var cached = comicItem.TitleComic.Metadata.ThumbnailSource;
            comicItem.TitleComic.Metadata.ThumbnailSource = path.GetPathRelativeTo(comicItem.TitleComic.Path);

            bool success;

            try {
                success = await Thumbnail.GenerateThumbnailAsync(comicItem.TitleComic, this.MainViewModel.Profile, replace: true);
            } catch (Exception e) {
                comicItem.TitleComic.Metadata.ThumbnailSource = cached;
                throw e;
            }


            if (success) {
                await this.MainViewModel.NotifyComicsChangedAsync(new[] { comicItem.TitleComic });
                comicItem.DoNotifyThumbnailChanged();
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

            await this.TryRedefineThumbnailAsync(comicItem, file.Path);
        }

        #endregion

        /* Instead of putting logic in each observable property's setter, we put them here, to keep setter code the
         * same for each property */
        private void ComicViewModel_PropertyChanged(object _, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(this.SelectedSortIndex):
                    Defaults.SettingsAccessor.SetLastSortSelection(this.navigationTag, this.SelectedSortIndex);

                    if ((Sorting.SortSelector)this.SelectedSortIndex == Sorting.SortSelector.Random) {
                        this.randomSortSelectors = this.randomSortSelectors.Keys.ToDictionary(e => e, _ => App.Randomizer.Next());
                    }

                    this.RefreshComicItems();
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

        private void MainViewModel_ComicsModified(MainViewModel sender, ComicsModifiedEventArgs e) {
            switch (e.ModificationType) {
                case ComicModificationType.ItemsAdded:
                    if (this.navigationTag == MainViewModel.SecondLevelNavigationTag) {
                        // We can't handle this. The parent will have handled it, though.
                        this.MainViewModel.NavigateOut();
                        return;
                    }

                    foreach (var comic in e.ModifiedComics) {
                        this.comics.Add(comic);
                    }

                    var newComicItems = e.ModifiedComics.Select(ComicItem.WorkItem).ToList();

                    /* If we are on the comics tab, we just need to add the comics to the view.
                     * If we are on another tab, though, we will need to be able to create new ComicItems on the fly
                     * to handle new item groups, and we say fuck that */
                    if (this.navigationTag == MainViewModel.DefaultNavigationTag) {
                        this.ComicItems.AddRange(newComicItems);
                    } else {
                        this.RefreshComicItems();
                    }

                    /* Generate thumbnails for added items */
                    /* There may be many view models active at any given moment. The if statement ensures that only
                     * the top level grid (guaranteed to be unique) requests thumbnails to be generated */
                    if (this.navigationTag != MainViewModel.SecondLevelNavigationTag) {
                        this.RequestGenerateThumbnails(newComicItems);
                    }

                    break;
                case ComicModificationType.ItemsChanged:
                    foreach (var comic in e.ModifiedComics) {
                        foreach (var item in this.ComicItems) {
                            if (item.TitleComic.UniqueIdentifier == comic.UniqueIdentifier) {
                                item.DoNotifyUnderlyingComicsChanged();
                            }
                        }
                    }
                    break;

                case ComicModificationType.ItemsRemoved:
                    // We remove these items directly from this.ComicItems without having to call RefreshComicItems
                    var removedUniqueIds = new HashSet<string>();

                    foreach (var item in e.ModifiedComics) {
                        this.comics.Remove(item);
                        removedUniqueIds.Add(item.UniqueIdentifier);
                    }

                    // comicitems
                    var removedComicItemIndices = new Stack<int>();
                    for (var i_comicItem = 0; i_comicItem < this.ComicItems.Count; i_comicItem++) {
                        var comicItem = this.ComicItems[i_comicItem];
                        var removedComicIndices = new Stack<int>();

                        for (var i_comic = 0; i_comic < comicItem.Comics.Count; i_comic++) {
                            if (removedUniqueIds.Contains(comicItem.Comics[i_comic].UniqueIdentifier)) {
                                removedComicIndices.Push(i_comic);
                            }
                        }

                        if (removedComicIndices.Count > 0) {
                            while (removedComicIndices.Count > 0) {
                                comicItem.Comics.RemoveAt(removedComicIndices.Pop());
                            }

                            if (comicItem.Comics.Count == 0) {
                                removedComicItemIndices.Push(i_comicItem);
                            } else {
                                comicItem.DoNotifyUnderlyingComicsChanged();
                            }
                        }

                    }

                    while (removedComicItemIndices.Count > 0) {
                        this.ComicItems.RemoveAt(removedComicItemIndices.Pop());
                    }

                    if (this.ComicItems.Count == 0) {
                        this.MainViewModel.NavigateOut();
                    }

                    break;
                default:
                    throw new ApplicationLogicException("Unhandled switch case");
            }
        }
    }
}
