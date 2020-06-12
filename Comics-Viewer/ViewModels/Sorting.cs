using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels {
    public static class Sorting {
        public enum SortSelector {
            Title, Author, DateAdded, ItemCount, Random
        }

        public static readonly string[] SortSelectorNames = { "Title", "Author", "Date Added", "Item Count", "Random" };

        public static List<ComicItem> Sorted(IEnumerable<ComicItem> items, SortSelector sortSelector) {
            if (sortSelector == SortSelector.Random) {
                throw new ApplicationLogicException("Random sorting must be handled manually by respective view models");
            }

            // Note: We don't actually use the original list ever again, but we probably don't need to optimize for, what,
            // a split-second of extra memory use?
            var copy = new List<ComicItem>(items);
            copy.Sort(ComicItemComparisonForSortSelector(sortSelector));
            return copy;
        }

        private static Comparison<ComicItem> ComicItemComparisonForSortSelector(SortSelector sortSelector) {
            return sortSelector switch {
                SortSelector.Title => CompareTitle,
                SortSelector.Author => CompareAuthor,
                SortSelector.DateAdded => CompareDateAdded,
                SortSelector.ItemCount => CompareItemCount,
                SortSelector.Random => throw new ApplicationLogicException("Random sort should not propagate here"),
                _ => throw new ApplicationLogicException("Theoretically unreachable code"),
            };
        }

        private static int CompareTitle(ComicItem a, ComicItem b) {
            return a.Title.CompareTo(b.Title);
        }

        private static int CompareAuthor(ComicItem a, ComicItem b) {
            return CompareSameType(a, b, ComicItemType.Work, comic => comic.TitleComic.DisplayAuthor, CompareTitle);
        }

        private static int CompareItemCount(ComicItem a, ComicItem b) {
            return CompareSameType(a, b, ComicItemType.Navigation, comic => comic.Comics.Count(), CompareAuthor, reverse: true);
        }

        private static int CompareDateAdded(ComicItem a, ComicItem b) {
            return CompareSameType(a, b, ComicItemType.Work, comic => comic.TitleComic.DateAdded, CompareAuthor, reverse: true);
        }

        private static int CompareSameType<T>(
            ComicItem a, ComicItem b, ComicItemType type, Func<ComicItem, T> key, Comparison<ComicItem> fallback, 
            bool reverse = false, bool sortBeforeOtherTypes = true
        ) where T : IComparable<T> {
            if (a.ItemType == type && b.ItemType == type) {
                var comparisonResult = key(a).CompareTo(key(b));
                if (comparisonResult != 0) {
                    if (reverse) {
                        return -comparisonResult;
                    }
                    return comparisonResult;
                }

                return fallback(a, b);
            }

            if (a.ItemType == type) {
                return sortBeforeOtherTypes ? 1 : -1;
            }

            if (b.ItemType == type) {
                return sortBeforeOtherTypes ? -1 : 1;
            }

            return fallback(a, b);
        }
    }
}
