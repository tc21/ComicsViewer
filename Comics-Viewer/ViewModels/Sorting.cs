using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels {
    static class Sorting {
        internal enum SortSelector {
            Title, Author, DateAdded, ItemCount, Random
        }

        internal static readonly string[] SortSelectorNames = { "Title", "Author", "Date Added", "Item Count", "Random" };

        internal static List<ComicItem> Sorted(List<ComicItem> items, SortSelector sortSelector) {
            if (sortSelector == SortSelector.Random) {
                return items.OrderBy(_ => App.Randomizer.Next()).ToList();
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
            return CompareSameType(a, b, (ComicWorkItem comic) => comic.Comic.Author, CompareTitle);
        }

        private static int CompareItemCount(ComicItem a, ComicItem b) {
            return CompareSameType(a, b, (ComicNavigationItem comic) => comic.Comics.Count, CompareAuthor, reverse: true);
        }

        private static int CompareDateAdded(ComicItem a, ComicItem b) {
            return CompareSameType(a, b, (ComicWorkItem comic) => comic.Comic.DateAdded, CompareAuthor, reverse: true);
        }

        private static int CompareSameType<U, T>(
            ComicItem a, ComicItem b, Func<U, T> key, Comparison<ComicItem> fallback,
            bool reverse = false, bool sortBeforeOtherTypes = true
        ) where U : ComicItem where T : IComparable<T> {
            if (a is U a_ && b is U b_) {
                var comparisonResult = key(a_).CompareTo(key(b_));
                if (comparisonResult != 0) {
                    if (reverse) {
                        return -comparisonResult;
                    }
                    return comparisonResult;
                }

                return fallback(a, b);
            }

            if (a is U) {
                return sortBeforeOtherTypes ? 1 : -1;
            }

            if (b is U) {
                return sortBeforeOtherTypes ? -1 : 1;
            }

            return fallback(a, b);
        }


    }
}
