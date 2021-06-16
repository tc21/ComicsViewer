using System;
using System.Collections.Generic;
using ComicsLibrary.Collections;
using ComicsViewer.Common;

namespace ComicsLibrary.Sorting {
    public enum ComicCollectionSortSelector {
        Name, ItemCount, Random
    }

    public static partial class SortSelectorNames {
        public static readonly string[] ComicCollectionSortSelectorNames = { "Name", "Item Count", "Random" };
    }

    public static class ComicCollectionComparers {
        private class NameComparer : IComparer<IComicCollection> {
            public int Compare(IComicCollection x, IComicCollection y) => CompareProperty(x, y);

            public static int CompareProperty(IComicCollection x, IComicCollection y) {
                return string.Compare(x.Name.ToLowerInvariant(), y.Name.ToLowerInvariant(), StringComparison.Ordinal);
            }
        }

        private class ItemCountComparer : IComparer<IComicCollection> {
            public int Compare(IComicCollection x, IComicCollection y) => CompareProperty(x, y);

            private static int CompareProperty(IComicCollection x, IComicCollection y) {
                var result = -x.Comics.Count.CompareTo(y.Comics.Count);
                if (result != 0) {
                    return result;
                }

                return NameComparer.CompareProperty(x, y);
            }
        }

        public static IComparer<IComicCollection> Make(ComicCollectionSortSelector sortSelector) {
            return sortSelector switch {
                ComicCollectionSortSelector.Name => new NameComparer(),
                ComicCollectionSortSelector.ItemCount => new ItemCountComparer(),
                ComicCollectionSortSelector.Random => throw new ProgrammerError(
                    $"{nameof(ComicCollectionComparers)}.{nameof(Make)}: Random not allowed here"
                ),
                _ => throw new ProgrammerError($"{nameof(ComicCollectionComparers)}.{nameof(Make)}: unexpected sort selector")
            };
        }
    }
}
