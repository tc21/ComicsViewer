using System;
using System.Collections.Generic;
using ComicsViewer.Common;

namespace ComicsLibrary.Sorting {
    public enum ComicSortSelector {
        Title, Author, DateAdded, Random
    }

    public static partial class SortSelectorNames {
        public static readonly string[] ComicSortSelectorNames = new[] { "Title", "Author", "Date Added", "Random" };
    }

    public static class ComicComparers {
        private class TitleComparer : IComparer<Comic> {
            public int Compare(Comic x, Comic y) => CompareComic(x, y);

            public static int CompareComic(Comic x, Comic y) {
                return string.Compare(x.DisplayTitle.ToLowerInvariant(), y.DisplayTitle.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private class AuthorComparer : IComparer<Comic> {
            public static int CompareComic(Comic x, Comic y) {
                var result = string.Compare(x.Author.ToLowerInvariant(), y.Author.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);

                if (result != 0) {
                    return result;
                }

                return TitleComparer.CompareComic(x, y);
            }

            public int Compare(Comic x, Comic y) => CompareComic(x, y);
        }

        private class DateAddedComparer : IComparer<Comic> {
            private static int CompareComic(Comic x, Comic y) {
                var result = -string.Compare(x.DateAdded, y.DateAdded, StringComparison.OrdinalIgnoreCase);

                if (result != 0) {
                    return result;
                }

                return AuthorComparer.CompareComic(x, y);
            }

            public int Compare(Comic x, Comic y) => CompareComic(x, y);
        }

        public static IComparer<Comic> Make(ComicSortSelector sortSelector) {
            return sortSelector switch {
                ComicSortSelector.Title => new TitleComparer(),
                ComicSortSelector.Author => new AuthorComparer(),
                ComicSortSelector.DateAdded => new DateAddedComparer(),
                ComicSortSelector.Random => throw new ProgrammerError($"{nameof(ComicComparers)}.{nameof(Make)}: Random is not allowed here"),
                _ => throw new ProgrammerError($"{nameof(ComicComparers)}.{nameof(Make)}: unexpected sort selector")
            };
        }
    }
}
