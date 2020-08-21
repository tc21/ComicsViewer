using System.Collections.Generic;

#nullable enable

namespace ComicsLibrary.Sorting {
    public enum ComicSortSelector {
        Title, Author, DateAdded, Random
    }

    public static class ComicComparers {
        private class TitleComparer : IComparer<Comic> {
            public int Compare(Comic x, Comic y) => CompareComic(x, y);

            public static int CompareComic(Comic x, Comic y) {
                return x.DisplayTitle.ToLowerInvariant().CompareTo(y.DisplayTitle.ToLowerInvariant());
            }
        }

        private class AuthorComparer : IComparer<Comic> {
            public static int CompareComic(Comic x, Comic y) {
                var result = x.DisplayAuthor.ToLowerInvariant().CompareTo(y.DisplayAuthor.ToLowerInvariant());
                if (result != 0) {
                    return result;
                }

                return TitleComparer.CompareComic(x, y);
            }

            public int Compare(Comic x, Comic y) => CompareComic(x, y);
        }

        private class DateAddedComparer : IComparer<Comic> {
            public static int CompareComic(Comic x, Comic y) {
                var result = -(x.DateAdded.CompareTo(y.DateAdded));
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
                _ => throw new ProgrammerError($"{nameof(ComicComparers)}.{nameof(Make)}: unexpected sort selector"),
            };
        }
    }
}
