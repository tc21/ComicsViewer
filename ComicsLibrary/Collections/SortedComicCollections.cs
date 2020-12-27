using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsLibrary.Collections {
    internal class SortedComicCollections : IReadOnlyCollection<IComicCollection> {
        // we'll use null to mark Random
        private readonly IComparer<IComicCollection>? comparer;
        public SortedComicCollections(ComicCollectionSortSelector sortSelector, IEnumerable<IComicCollection> initialItems) {
            var items = initialItems.ToList();

            if (sortSelector == ComicCollectionSortSelector.Random) {
                this.comparer = null;
                General.Shuffle(items);
            } else {
                this.comparer = ComicCollectionComparers.Make(sortSelector);
                items.Sort(this.comparer);
            }

            this.Initialize(items);
        }

        // this is a useless node that exists to simplify our code
        private readonly Node head = new(default);
        private readonly Dictionary<string, Node> properties = new();

        // Note: for some reason we'd get an ExecutionEngineException when Count was pointed to this.properties.Count,
        // so we're doing it manually
        public int Count { get; private set; }

        private void Initialize(IEnumerable<IComicCollection> items) {
            var current = head;
            foreach (var item in items) {
                var next = new Node(item);
                this.properties.Add(item.Name, next);
                this.Count += 1;

                current.Next = next;
                next.Prev = current;
                current = next;
            }
        }

        public void Add(IComicCollection property) {
            var current = this.head;
            var node = new Node(property);

            // advance to before where we insert the new item 
            if (this.comparer is null) {
                var index = General.Randomizer.Next(0, this.Count + 1);

                while (index-- > 0) {
                    current = current.Next!;
                }
            } else {
                while (current.Next is { } _next && this.comparer.Compare(_next.Value!, property) < 0) {
                    current = current.Next;
                }
            }

            if (current.Next is { } next) {
                next.Prev = node;
                node.Next = next;
            }

            node.Prev = current;
            current.Next = node;

            properties.Add(property.Name, node);
            this.Count += 1;
        }

        public void Clear() {
            this.head.Next = null;
            this.properties.Clear();
            this.Count = 0;
        }

        public IComicCollection Remove(string property) {
            var node = properties[property];
            _ = properties.Remove(property);
            this.Count -= 1;

            if (node.Next is { } next) {
                next.Prev = node.Prev;
            }

            if (node.Prev is { } prev) {
                prev.Next = node.Next;
            } else {
                throw new ProgrammerError();
            }

            return node.Value!;
        }

        public bool Contains(string property) => this.properties.ContainsKey(property);

        public IComicCollection Get(string property) => this.properties[property].Value!;

        public IEnumerator<IComicCollection> GetEnumerator() {
            var current = this.head;

            while (current.Next is { } next) {
                yield return next.Value!;
                current = next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private class Node {
            public Node? Prev;
            public Node? Next;
            public IComicCollection? Value;

            public Node(IComicCollection? value) {
                this.Value = value;
            }
        }
    }
}
