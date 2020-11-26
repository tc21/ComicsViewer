using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsLibrary.Collections {
    public interface IComicProperty {
        public string Name { get; }
        public IEnumerable<Comic> Comics { get; }
    }

    public class ComicProperty : IComicProperty {
        public string Name { get; }
        public ComicView Comics { get; }

        IEnumerable<Comic> IComicProperty.Comics => this.Comics;

        public ComicProperty(string name, ComicView comics) {
            this.Comics = comics;
            this.Name = name;
        }
    }

    internal class SortedPropertyCollection : IReadOnlyCollection<ComicProperty> {
        private readonly IComparer<IComicProperty> comparer;
        public SortedPropertyCollection(ComicPropertySortSelector sortSelector) {
            this.comparer = ComicPropertyComparers.Make(sortSelector);
        }

        // this is a useless node that exists to simplify our code
        private readonly Node head = new(new("<error>", ComicView.Empty));
        private readonly Dictionary<string, Node> properties = new();

        public int Count => this.properties.Count;

        public void Add(ComicProperty property) {
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

        public ComicProperty Remove(string property) {
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

        public IEnumerator<ComicProperty> GetEnumerator() {
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
            public ComicProperty Value;

            public Node(ComicProperty value) {
                this.Value = value;
            }
        }
    }

    public class ComicPropertiesView : IReadOnlyCollection<ComicProperty> {
        private readonly ComicView parent;
        private readonly Func<Comic, IEnumerable<string>> getProperties;

        private ComicPropertySortSelector sortProperty;
        private readonly SortedPropertyCollection properties;

        public int Count => this.properties.Count;

        public ComicPropertiesView(ComicView parent, Func<Comic, IEnumerable<string>> getProperties) {
            this.parent = parent;
            this.getProperties = getProperties;
            this.properties = new SortedPropertyCollection(this.sortProperty);

            parent.ViewChanged += this.ParentComicView_ViewChanged;

            this.InitializeProperties();
        }

        public void Sort(ComicPropertySortSelector sortProperty) {
            this.sortProperty = sortProperty;

            this.properties.Clear();
            this.InitializeProperties();
        }

        private void ParentComicView_ViewChanged(ComicView sender, ComicView.ViewChangedEventArgs e) {
            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                    var addedProperties = new HashSet<string>();
                    var modifiedProperties = new HashSet<string>();
                    var removedProperties = new HashSet<string>();

                    // When a comic is modified, it is removed, then added. Thus we process removals first.
                    // e.Remove is different from e.Add: e.Remove is the "before" comics, and e.Add is the "after".
                    var propertiesOfRemovedComics = new HashSet<string>(e.Remove.SelectMany(this.getProperties));
                    foreach (var property in propertiesOfRemovedComics) {
                        var propertyView = this.properties.Remove(property);

                        if (propertyView.Comics.Any()) {
                            this.properties.Add(propertyView);

                            _ = modifiedProperties.Add(property);
                        } else {
                            _ = removedProperties.Add(property);
                        }
                    }

                    var propertiesOfAddedComics = new HashSet<string>(e.Add.SelectMany(this.getProperties));
                    foreach (var property in propertiesOfAddedComics) {
                        if (modifiedProperties.Contains(property)) {
                            // do nothing
                        } else {
                            var view = this.parent.Filtered(comic => getProperties(comic).Contains(property));
                            this.properties.Add(new ComicProperty(property, view));

                            _ = addedProperties.Add(property);
                        }
                    }

                    this.PropertiesChanged?.Invoke(this, new(PropertiesChangeType.ItemsChanged, addedProperties, modifiedProperties, removedProperties));

                    break;
                case ComicChangeType.ThumbnailChanged:
                    break;
                case ComicChangeType.Refresh:
                    this.properties.Clear();
                    this.InitializeProperties();

                    break;

                default:
                    throw new ProgrammerError("unhandled switch case");
            }
        }

        private void InitializeProperties() {
            var propertyNames = new HashSet<string>();

            foreach (var comic in this.parent) {
                propertyNames.UnionWith(getProperties(comic));
            }

            foreach (var propertyName in propertyNames) {
                var view = this.parent.Filtered(comic => getProperties(comic).Contains(propertyName));
                this.properties.Add(new ComicProperty(propertyName, view));
            }

            this.PropertiesChanged?.Invoke(this, new PropertiesChangedEventArgs(PropertiesChangeType.Refresh, this.Select(p => p.Name)));
        }

        public IEnumerator<ComicProperty> GetEnumerator() {
            return this.properties.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        /// <summary>
        /// The PropertiesChanged event is propagated down to children views so that external classes using a ComicPropertiesView can
        /// update their information about properties that have changed in this view.
        /// </summary> 
        public event PropertiesChangedEventHandler? PropertiesChanged;
        public delegate void PropertiesChangedEventHandler(ComicPropertiesView sender, PropertiesChangedEventArgs e);
    }

    public class PropertiesChangedEventArgs {
        public readonly PropertiesChangeType Type;
        public readonly IEnumerable<string> Added;
        public readonly IEnumerable<string> Modified;
        public readonly IEnumerable<string> Removed;

        internal PropertiesChangedEventArgs(PropertiesChangeType type, IEnumerable<string>? added = null,
                                            IEnumerable<string>? modified = null, IEnumerable<string>? removed = null) {
            this.Type = type;
            this.Added = added ?? Array.Empty<string>();
            this.Modified = modified ?? Array.Empty<string>();
            this.Removed = removed ?? Array.Empty<string>();
        }
    }

    public enum PropertiesChangeType {
        ItemsChanged, Refresh
    }
}
