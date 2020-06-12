using ComicsLibrary;
using ComicsViewer.Filters;
using ComicsViewer.Profiles;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
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
        public List<ComicItem> ComicItems { get; private set; } = new List<ComicItem>();
        internal void SetComicItems(List<ComicItem> items) {
            if (items == this.ComicItems) {
                return;
            }

            this.ComicItems = items;
            this.OnPropertyChanged(nameof(this.ComicItems));
            this.OnPropertyChanged(nameof(this.VisibleItemCount));
        }

        /* manually managed properties */
        public string[] SortSelectors => Sorting.SortSelectorNames;
        public int ImageHeight => this.MainViewModel.Profile.ImageHeight;
        public int ImageWidth => this.MainViewModel.Profile.ImageWidth;
        public string ProfileName => this.MainViewModel.Profile.Name;
        public int VisibleItemCount => this.ComicItems.Count;

        internal readonly MainViewModel MainViewModel;
        private readonly List<Comic> comics;
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
            this.comics = comics.ToList();
            this.navigationTag = appViewModel.ActiveNavigationTag;

            // Note: please keep this line before setting SelectedSortIndex...
            this.PropertyChanged += this.ComicViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged += this.AppViewModel_ProfileChanged;
            this.MainViewModel.Filter.FilterChanged += this.Filter_FilterChanged;

            var allComicItems = CreateComicItems(this.comics, null, this.navigationTag);
            this.randomSortSelectors = allComicItems.Select(item => item.Title).Distinct().ToDictionary(e => e, _ => 0);

            this.SelectedSortIndex = Defaults.SettingsAccessor.GetLastSortSelection(this.MainViewModel.ActiveNavigationTag);

            // Loads the actual comic items
            this.RefreshComicItems();
        }

        private void RefreshComicItems() {
            var comicItems = CreateComicItems(comics, this.MainViewModel.Filter, this.navigationTag);

            if ((Sorting.SortSelector)this.SelectedSortIndex == Sorting.SortSelector.Random) {
                this.SetComicItems(comicItems.OrderBy(i => this.randomSortSelectors[i.Title]).ToList());
            } else {
                this.SetComicItems(Sorting.Sorted(comicItems, (Sorting.SortSelector)this.SelectedSortIndex));
            }
        }

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

        // Helper function to open GridView.SelectedItems
        internal Task OpenItemsAsync(IEnumerable<object> items) {
            return this.OpenItemsAsync(items.Cast<ComicItem>());
        }

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
                var tasks = items.Select(item => Startup.OpenComicAsync(item.TitleComic, this.MainViewModel.Profile));
                await Task.WhenAll(tasks);
            } catch (UnauthorizedAccessException) {
                _ = await new MessageDialog("Please enable file system access in settings to open comics.", "Access denied").ShowAsync();
            } catch (FileNotFoundException) {
                _ = await new MessageDialog("The application is not currently capable of handling this error.", "File not found").ShowAsync();
            }
        }

        /* Instead of putting logic in each observable property's setter, we put them here, to keep setter code the
         * same for each property */
        private void ComicViewModel_PropertyChanged(object _, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(this.SelectedSortIndex):
                    Defaults.SettingsAccessor.SetLastSortSelection(this.MainViewModel.ActiveNavigationTag, this.SelectedSortIndex);

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

        private void AppViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            this.OnPropertyChanged(nameof(this.ImageHeight));
            this.OnPropertyChanged(nameof(this.ImageWidth));
            this.OnPropertyChanged(nameof(this.ProfileName));
        }
    }
}
