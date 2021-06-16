using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ComicsLibrary.Sorting;

namespace ComicsLibrary.Collections {
    internal class SortedComicCollections: IReadOnlyCollection<IComicCollection> {
        private readonly IComparer<IComicCollection>? comparer;
        private readonly List<IComicCollection> items = new();
        private readonly Dictionary<string, IComicCollection> itemsDictionary = new();

        public SortedComicCollections(ComicCollectionSortSelector sortSelector, IEnumerable<IComicCollection> initialItems) {
            var items = initialItems.ToList();

            if (sortSelector == ComicCollectionSortSelector.Random) {
                this.comparer = null;
                General.Shuffle(items);
            } else {
                this.comparer = ComicCollectionComparers.Make(sortSelector);
                items.Sort(this.comparer);
            }

            foreach (var item in initialItems) {
                this.Add(item);
            }
        }

        public int Count => this.items.Count;

        public void Add(IComicCollection collection) {
            if (this.Contains(collection)) {
                throw new ArgumentException($"Key already exists: {collection.Name}");
            }

            this.items.Insert(this.BinarySearch(collection), collection);
            this.itemsDictionary.Add(collection.Name, collection);
        }

        public IComicCollection Remove(string name) {
            if (this.IndexOf(name) is not { } index) {
                throw new KeyNotFoundException($"Key not found: {name}");
            }

            var result = this.items[index];
            this.items.RemoveAt(index);
            _ = this.itemsDictionary.Remove(name);
            return result;
        }

        public void Clear() {
            this.items.Clear();
            this.itemsDictionary.Clear();
        }

        public bool Contains(string name) => this.IndexOf(name) is not null;
        public bool Contains(IComicCollection collection) => this.Contains(collection.Name);

        public IComicCollection Get(string name) {
            return this.itemsDictionary[name];
        }

        public int? IndexOf(string name) {
            if (this.comparer is null) {
                return this.items.FindIndex(coll => coll.Name == name) switch {
                    -1 => null,
                    int x => x
                };
            }

            if (this.itemsDictionary.TryGetValue(name, out var collection)) {
                return this.BinarySearch(collection);
            }

            return null;
        }

        public IEnumerator<IComicCollection> GetEnumerator() => this.items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private int BinarySearch(IComicCollection name) {
            if (this.comparer is { } comparer) {
                var result = this.items.BinarySearch(name, comparer);

                return result switch {
                    < 0 => ~result,
                    _ => result
                };
            }

            return 0;
        }
    }
}
