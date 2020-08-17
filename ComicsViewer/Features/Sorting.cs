using ComicsLibrary;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.Features {
    public static class Sorting {
        public enum SortSelector {
            Title, Author, DateAdded, ItemCount, Random
        }

        public static readonly string[] SortSelectorNames = { "Title", "Author", "Date Added", "Item Count", "Random" };
        
        // Attempts to lazily create ComicItems, unless the selected combination of sortSelector and navigationTag
        // does not allow us to do so. Note: will sort the parameter comics in place.
        public static IEnumerable<ComicItem> SortAndCreateComicItems(
            List<Comic> comics, SortSelector sortSelector, string navigationTag, 
            ViewModels.Pages.MainViewModel trackChangesFrom, 
            ComicItem.RequestingRefreshEventArgs onRequestingRefresh
        ) {
            if (comics.Count == 0) {
                return new ComicItem[0];
            }

            if (navigationTag == ViewModels.Pages.MainViewModel.DefaultNavigationTag ||
                    navigationTag == ViewModels.Pages.MainViewModel.SecondLevelNavigationTag) {
                // this means we are only showing work items in this page, which allows us to take a different set of shortcuts.
                switch (sortSelector) {
                    case SortSelector.Title:
                        comics.Sort(CompareComicTitle);
                        break;
                    case SortSelector.ItemCount:  // item count is useless for work items
                    case SortSelector.Author:
                        comics.Sort(CompareComicAuthorThenTitle);
                        break;
                    case SortSelector.DateAdded:
                        comics.Sort(CompareComicDateAddedThenAuthorAndTitle);
                        break;
                    case SortSelector.Random:
                        Shuffle(comics);
                        break;
                }

                return comics.Select(comic => {
                    var item = ComicItem.WorkItem(comic, trackChangesFrom);
                    item.RequestingRefresh += onRequestingRefresh;
                    return item;
                });
            }

            // only nav items

            // we achieve lazy loading of ComicItems by sorting the list exactly the way we want for
            // a limited amout of situations.
            // Author and DateAdded are not used for sorting nav items, so they're treated the same as Title.
            // for tags, we aren't really going to implement this algorithm since it's probably not worth it
            // for ItemCount and Random, we have no choice but to create all items first

            var canSortBeforeGrouping = 
                (sortSelector == SortSelector.Title || sortSelector == SortSelector.Author || sortSelector == SortSelector.DateAdded)
                && (navigationTag == "authors" || navigationTag == "categories");

            return navigationTag switch {
                "authors" => SortAndGroup(comics, comic => comic.DisplayAuthor, canSortBeforeGrouping, trackChangesFrom, onRequestingRefresh),
                "categories" => SortAndGroup(comics, comic => comic.DisplayCategory, canSortBeforeGrouping, trackChangesFrom, onRequestingRefresh),
                "tags" => SortAndGroupByMultiple(comics, comic => comic.Tags, trackChangesFrom, onRequestingRefresh),
                _ => throw new ApplicationLogicException($"Unexpected navigation tag '{navigationTag}' when creating comic items."),
            };
    }

        private static IEnumerable<ComicItem> SortAndGroup(
            List<Comic> comics, Func<Comic, string> getGroupName, bool sortBeforeGrouping,
            ViewModels.Pages.MainViewModel trackChangesFrom,
            ComicItem.RequestingRefreshEventArgs onRequestingRefresh
        ) {
            // capitalizaton is ignored; the casing of the first item is used as the final returned result 
            if (sortBeforeGrouping) {
                return SortBeforeAndGroup(comics);
            }

            var dict = new Dictionary<string, (string name, List<Comic> comics)>();

            foreach (var comic in comics) {
                var groupName = getGroupName(comic);
                var groupKey = groupName.ToLowerInvariant();

                if (!dict.ContainsKey(groupKey)) {
                    dict[groupKey] = (groupName, new List<Comic> { comic });
                } else {
                    dict[groupKey].comics.Add(comic);
                }
            }

            var sortedKeys = dict.Keys.ToList();
            sortedKeys.Sort();

            return sortedKeys.Select(key => navigationItem(dict[key].name, dict[key].comics));


            ComicItem navigationItem(string name, List<Comic> comics) {
                var item = ComicItem.NavigationItem(name, comics, trackChangesFrom);
                item.RequestingRefresh += onRequestingRefresh;
                return item;
            }

            IEnumerable<ComicItem> SortBeforeAndGroup(List<Comic> comics) {
                var comicsWithSortName = comics.Select(comic => (sortName: getGroupName(comic).ToLowerInvariant(), comic)).ToList();
                comicsWithSortName.Sort((a, b) => a.sortName.CompareTo(b.sortName));

                var currentSortName = comicsWithSortName[0].sortName;
                var currentGroup = new List<Comic> { comicsWithSortName[0].comic };

                foreach (var (sortName, comic) in comicsWithSortName.Skip(1)) {
                    if (sortName != currentSortName) {
                        yield return navigationItem(getGroupName(currentGroup[0]), currentGroup);

                        currentSortName = sortName;
                        currentGroup = new List<Comic> { comic };
                    } else {
                        currentGroup.Add(comic);
                    }
                }

                yield return navigationItem(getGroupName(currentGroup[0]), currentGroup);
            }
        }

        private static IEnumerable<ComicItem> SortAndGroupByMultiple(List<Comic> comics, Func<Comic, IEnumerable<string>> getGroupNames,
            ViewModels.Pages.MainViewModel trackChangesFrom,
            ComicItem.RequestingRefreshEventArgs onRequestingRefresh
        ) {
            // capitalizaton is ignored; the casing of the first item is used as the final returned result 
            var dict = new Dictionary<string, (string name, List<Comic> comics)>();

            foreach (var comic in comics) {
                foreach (var groupName in getGroupNames(comic)) {
                    var groupKey = groupName.ToLowerInvariant();

                    if (!dict.ContainsKey(groupKey)) {
                        dict[groupKey] = (groupName, new List<Comic> { comic });
                    } else {
                        dict[groupKey].comics.Add(comic);
                    }
                }
            }

            foreach (var pair in dict) {
                var item = ComicItem.NavigationItem(pair.Value.name, pair.Value.comics, trackChangesFrom);
                item.RequestingRefresh += onRequestingRefresh;
                yield return item;
            }
        }

        private static int CompareComicTitle(Comic a, Comic b) {
            return a.DisplayTitle.CompareTo(b.DisplayTitle);
        }

        private static int CompareComicAuthorThenTitle(Comic a, Comic b) {
            var result = a.DisplayAuthor.CompareTo(b.DisplayAuthor);
            if (result != 0) {
                return result;
            }

            return CompareComicTitle(a, b);
        }

        private static int CompareComicDateAddedThenAuthorAndTitle(Comic a, Comic b) {
            var result = a.DateAdded.CompareTo(b.DateAdded);
            if (result != 0) {
                // note: this minus sign is intentional
                return -result;
            }

            return CompareComicAuthorThenTitle(a, b);
        }

        // Of course, there's no way to shuffle a list without generating all values.
        // Again, shuffles in place.
        private static void Shuffle<T>(List<T> list) {
            // Fisher-Yates
            for (var i = list.Count - 1; i > 0; i--) {
                var random = App.Randomizer.Next(i + 1);
                var temp = list[i];
                list[i] = list[random];
                list[random] = temp;
            }
        }
    }
}
