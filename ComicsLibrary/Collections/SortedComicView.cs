using ComicsLibrary.Sorting;
using System;
using System.Collections.Generic;

#nullable enable

namespace ComicsLibrary.Collections {
    /// <summary>
    /// A comic view that can be sorted in-place.
    /// </summary>
    public class SortedComicView : MutableComicView {
        private readonly Dictionary<string, Comic> comicAccessor;  // allows for O(1) access of sortedComics
        private readonly List<Comic> sortedComics;
        private ComicSortSelector sortSelector;

        /// <summary>
        /// A convenience constructor, for a pre-populated comicAccessor field
        /// </summary>
        private SortedComicView(
            ComicView trackChangesFrom,
            Dictionary<string, Comic> comicAccessor,
            List<Comic> sortedComics,
            ComicSortSelector sortSelector
        ) : base(trackChangesFrom) {
            this.comicAccessor = comicAccessor;
            this.sortedComics = sortedComics;
            this.sortSelector = sortSelector;
        }

        internal SortedComicView(
            ComicView trackChangesFrom,
            IEnumerable<Comic> comics,
            ComicSortSelector sortSelector
        ) : this(trackChangesFrom, comicAccessor: new Dictionary<string, Comic>(), sortedComics: new List<Comic>(), sortSelector) {
            foreach (var comic in comics) {
                this.Instance_AddComic(comic);
            }
        }

        public void Sort(ComicSortSelector sortSelector) {
            this.sortSelector = sortSelector;
            SortComics(this.sortedComics, sortSelector);
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.Refresh));
        }

        private static void SortComics(List<Comic> comics, ComicSortSelector sortSelector) {
            if (sortSelector == ComicSortSelector.Random) {
                General.Shuffle(comics);
            } else {
                comics.Sort(ComicComparers.Make(sortSelector));
            }
        }

        // this override optimizes Sorted since we know more than an unsorted collection
        public override SortedComicView Sorted(ComicSortSelector sortSelector) {
            if (sortSelector == this.sortSelector) {
                return this;
            }

            var sorted = new List<Comic>(this.sortedComics);
            SortComics(sorted, sortSelector);

            return new SortedComicView(this, this.comicAccessor, sorted, sortSelector);
        }

        private void Instance_AddComic(Comic comic) {
            if (this.Contains(comic)) {
                throw new ArgumentException("comic already exists in this collection");
            }

            int index;

            if (this.sortSelector == ComicSortSelector.Random) {
                index = General.Randomizer.Next(this.Count() + 1);
            } else {
                index = this.sortedComics.BinarySearch(comic, ComicComparers.Make(this.sortSelector));
                if (index <= 0) {
                    index = ~index;
                }
            }

            this.sortedComics.Insert(index, comic);
            this.comicAccessor[comic.UniqueIdentifier] = comic;
        }

        private protected override void AddComic(Comic comic) {
            this.Instance_AddComic(comic);
        }

        private protected override void RemoveComic(Comic comic) {
            if (this.sortSelector == ComicSortSelector.Random) {
                if (!this.sortedComics.Remove(comic)) {
                    throw new ArgumentException("comic doesn't exist in this collection");
                }
            } else {
                var index = this.sortedComics.BinarySearch(comic, ComicComparers.Make(this.sortSelector));
                if (index < 0) {
                    throw new ArgumentException("comic doesn't exist in this collection");
                }
                this.sortedComics.RemoveAt(index);
            }

            _ = this.comicAccessor.Remove(comic.UniqueIdentifier);
        }

        private protected override void RefreshComics(IEnumerable<Comic> comics) {
            this.sortedComics.Clear();
            this.comicAccessor.Clear();

            foreach (var comic in comics) {
                this.AddComic(comic);
            }
        }

        public override bool Contains(Comic comic) => this.comicAccessor.ContainsKey(comic.UniqueIdentifier);
        public override Comic GetStored(Comic comic) => this.comicAccessor[comic.UniqueIdentifier];
        public override int Count() => this.sortedComics.Count;
        public override IEnumerator<Comic> GetEnumerator() => this.sortedComics.GetEnumerator();
    }
}
