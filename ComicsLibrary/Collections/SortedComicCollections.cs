using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using System.Collections;
using System.Collections.Generic;

#nullable enable

namespace ComicsLibrary.Collections {
    internal class SortedComicCollections : IReadOnlyCollection<ComicCollection> {
        private readonly IComparer<IComicCollection> comparer;
        public SortedComicCollections(ComicCollectionSortSelector sortSelector) {
            this.comparer = ComicCollectionComparers.Make(sortSelector);
        }

        // this is a useless node that exists to simplify our code
        private readonly Node head = new(new("<error>", ComicView.Empty));
        private readonly Dictionary<string, Node> properties = new();

        public int Count => this.properties.Count;

        public void Add(ComicCollection property) {
            var current = this.head;
            var node = new Node(property);

            // if (next is not null, and should be sorted before property
            while (current.Next is { } _next && this.comparer.Compare(_next.Value, property) < 0) {
                current = current.Next;
            }

            if (current.Next is { } next) {
                next.Prev = node;
                node.Next = next;
            }

            node.Prev = current;
            current.Next = node;

            properties.Add(property.Name, node);
        }

        public void Clear() {
            this.head.Next = null;
            this.properties.Clear();
        }

        public ComicCollection Remove(string property) {
            var node = properties[property];
            _ = properties.Remove(property);

            if (node.Next is { } next) {
                next.Prev = node.Prev;
            }

            if (node.Prev is { } prev) {
                prev.Next = node.Next;
            } else {
                throw new ProgrammerError();
            }

            return node.Value;
        }

        public bool Contains(string property) => this.properties.ContainsKey(property);

        public IEnumerator<ComicCollection> GetEnumerator() {
            var current = this.head;

            while (current.Next is { } next) {
                yield return next.Value;
                current = next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private class Node {
            public Node? Prev;
            public Node? Next;
            public ComicCollection Value;

            public Node(ComicCollection value) {
                this.Value = value;
            }
        }
    }
}
