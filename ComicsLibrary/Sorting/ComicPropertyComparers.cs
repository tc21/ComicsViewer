using ComicsLibrary.Collections;
using ComicsViewer.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ComicsLibrary.Sorting {
    public enum ComicPropertySortSelector {
        Name, ItemCount, Random
    }

    public static partial class SortSelectorNames {
        public static readonly string[] ComicPropertySortSelectorNames = { "Name", "Item Count", "Random" };
    }

    public static class ComicPropertyComparers {
        private class NameComparer : IComparer<IComicProperty> {
            public int Compare(IComicProperty x, IComicProperty y) => CompareProperty(x, y);

            public static int CompareProperty(IComicProperty x, IComicProperty y) {
                return string.Compare(x.Name.ToLowerInvariant(), y.Name.ToLowerInvariant(), StringComparison.Ordinal);
            }
        }

        private class ItemCountComparer : IComparer<IComicProperty> {
            public int Compare(IComicProperty x, IComicProperty y) => CompareProperty(x, y);

            private static int CompareProperty(IComicProperty x, IComicProperty y) {
                var result = -x.Comics.Count().CompareTo(y.Comics.Count());
                if (result != 0) {
                    return result;
                }

                return NameComparer.CompareProperty(x, y);
            }
        }

        public static IComparer<IComicProperty> Make(ComicPropertySortSelector sortSelector) {
            return sortSelector switch {
                ComicPropertySortSelector.Name => new NameComparer(),
                ComicPropertySortSelector.ItemCount => new ItemCountComparer(),
                ComicPropertySortSelector.Random => throw new ProgrammerError(
                    $"{nameof(ComicPropertyComparers)}.{nameof(Make)}: Random not allowed here"
                ),
                _ => throw new ProgrammerError($"{nameof(ComicPropertyComparers)}.{nameof(Make)}: unexpected sort selector")
            };
        }
    }
}
