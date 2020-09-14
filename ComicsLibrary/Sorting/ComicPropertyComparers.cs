using ComicsLibrary.Collections;
using ComicsViewer.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicsLibrary.Sorting {
    public enum ComicPropertySortSelector {
        Name, ItemCount, Random
    }

    public static partial class SortSelectorNames {
        public static readonly string[] ComicPropertySortSelectorNames = new[] { "Name", "Item Count", "Random" };
    }

    public class ComicPropertyComparers {
        private class NameComparer : IComparer<ComicProperty> {
            public int Compare(ComicProperty x, ComicProperty y) => CompareProperty(x, y);

            public static int CompareProperty(ComicProperty x, ComicProperty y) {
                return x.Name.ToLowerInvariant().CompareTo(y.Name.ToLowerInvariant());
            }
        }

        private class ItemCountComparer : IComparer<ComicProperty> {
            public int Compare(ComicProperty x, ComicProperty y) => CompareProperty(x, y);

            public static int CompareProperty(ComicProperty x, ComicProperty y) {
                var result = -(x.Comics.Count.CompareTo(y.Comics.Count));
                if (result != 0) {
                    return result;
                }

                return NameComparer.CompareProperty(x, y);
            }
        }

        public static IComparer<ComicProperty> Make(ComicPropertySortSelector sortSelector) {
            return sortSelector switch {
                ComicPropertySortSelector.Name => new NameComparer(),
                ComicPropertySortSelector.ItemCount => new ItemCountComparer(),
                _ => throw new ProgrammerError($"{nameof(ComicPropertyComparers)}.{nameof(Make)}: unexpected sort selector"),
            };
        }
    }
}
