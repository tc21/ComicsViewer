﻿using ComicsLibrary.Sorting;
using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace ComicsLibrary.Collections {
    /// <summary>
    /// A comic view that can be sorted in-place.
    /// </summary>
    public class SortedComicView : MutableComicView {
        private readonly Dictionary<string, Comic> comicAccessor;  // allows for O(1) access of sortedComics
        private readonly List<Comic> sortedComics;
        private readonly ComicSortSelector sortSelector;

        internal SortedComicView(
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
                this.AddComic(comic);
            }
        }

        public void Sort(ComicSortSelector sortSelector) {
            SortComics(this.sortedComics, sortSelector);
            // TODO we might make a "Sorted" enum, so that children can ignore this event,
            // but you can't change the sort of an invisible grid anyway
            this.OnComicChanged(new ViewChangedEventArgs(ChangeType.Refresh));
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

        private protected override void AddComic(Comic comic) {
            if (this.Contains(comic)) {
                throw new ArgumentException("comic already exists in this collection");
            }

            int index;

            if (sortSelector == ComicSortSelector.Random) {
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

        private protected override void RemoveComic(Comic comic) {
            if (sortSelector == ComicSortSelector.Random) {
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
