using ComicsLibrary.Sorting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace ComicsLibrary.Collections {
    public class ComicProperty {
        public string Name { get; }
        public IReadOnlyList<Comic> Comics { get; }

        public ComicProperty(string name, IReadOnlyList<Comic> comics) {
            this.Comics = comics;
            this.Name = name;
        }
    }

    /// <summary>
    /// A ComicPropertiesView represents a collection of lists of comics, where each list represents a comics that
    /// have the same property. This property represents authors, categories, and tags in ComicsViewer.
    /// For convenience, we refer to the name of a property as a "key".
    /// 
    /// <para>
    /// A ComicPropertiesView exists in a ComicView hierarchy, but its direct parent and children can only be 
    /// ComicViews not ComicPropertyViews.
    /// 
    /// </para>
    /// <para>
    /// We have no need for non-sorted, or modifiable ComicPropertyViews, so this is the only class of its kind.
    /// 
    /// </para>
    /// </summary>
    public class SortedComicPropertiesView : IEnumerable<ComicProperty>, IReadOnlyCollection<ComicProperty> {
        /* optimization notes:
         * - we can simplify some calls by allowing a variant of a Func<Comic, string> getProperties
         * - we can make our children more efficent with a custom ComicPropertyView : ComicView subclass instead of filtering from parent;
         * 
         * but we don't need these optimizations yet at the scale we're at. */
        private readonly ComicView parent;
        // As an implementation detail, the List<Comic> stored in accessor and the IReadOnlyList<Comic> stored in sortedComicProperties are the same list
        private readonly Dictionary<string, List<Comic>> accessor = new Dictionary<string, List<Comic>>();
        private readonly List<ComicProperty> sortedComicProperties = new List<ComicProperty>();

        /* when we remove an item, we have to remove its previous mappings, but that information isn't provided in a
         * ViewChangedEventArgs, so we have to remember it ourselves. */
        private readonly Dictionary<string, HashSet<string>> savedProperties = new Dictionary<string, HashSet<string>>();

        private readonly Func<Comic, IEnumerable<string>> getProperties;
        private ComicPropertySortSelector sortSelector;

        public SortedComicPropertiesView(ComicView parent, Func<Comic, IEnumerable<string>> getProperties, ComicPropertySortSelector sortSelector) {
            this.parent = parent;
            this.getProperties = getProperties;
            this.sortSelector = sortSelector;

            foreach (var comic in parent) {
                this.AddComic(comic);
            }

            parent.ViewChanged += this.Parent_ViewChanged;
        }

        public ComicView PropertyView(string property) {
            // used to throw an ArgumentException if property doesn't exist
            _ = this[property];

            return this.parent.Filtered(c => this.getProperties(c).Contains(property));
        }

        /// <summary>
        /// Sorts this view in-place. At the end of the sort, a <see cref="ComicPropertiesChanged"/> event is fired
        /// with type <see cref="ComicPropertyChangeType.Refresh"/>.
        /// </summary>
        public void Sort(ComicPropertySortSelector sortSelector) {
            this.sortSelector = sortSelector;

            if (sortSelector == ComicPropertySortSelector.Random) {
                General.Shuffle(this.sortedComicProperties);
            } else {
                this.sortedComicProperties.Sort(ComicPropertyComparers.Make(sortSelector));
            }

            this.ComicPropertiesChanged?.Invoke(this, new ComicPropertiesChangedEventArgs(ComicPropertyChangeType.Refresh));
        }

        private void AddProperty(string name, List<Comic> comics) {
            if (this.ContainsProperty(name)) {
                throw new ProgrammerError("comic already exists in this collection");
            }

            var property = new ComicProperty(name, comics);

            int index;

            if (this.sortSelector == ComicPropertySortSelector.Random) {
                index = General.Randomizer.Next(this.Count + 1);
            } else {
                index = this.sortedComicProperties.BinarySearch(property, ComicPropertyComparers.Make(this.sortSelector));
                if (index <= 0) {
                    index = ~index;
                }
            }

            this.sortedComicProperties.Insert(index, property);
            this.accessor[property.Name] = comics;
        }

        private void RemoveProperty(string name) {
            if (this.sortSelector == ComicPropertySortSelector.Random) {
                var index = this.sortedComicProperties.FindIndex(cp => cp.Name == name);
                if (index == -1) {
                    throw new ArgumentException("property doesn't exist in this collection");
                }
                this.sortedComicProperties.RemoveAt(index);
            } else {
                this.sortedComicProperties.RemoveAt(this.GetIndexOfExistingPropertyNotRandomSortOrder(name));
            }

            _ = this.accessor.Remove(name);
        }

        private int GetIndexOfExistingPropertyNotRandomSortOrder(string name) {
            var property = this[name];
            var index = this.sortedComicProperties.BinarySearch(property, ComicPropertyComparers.Make(this.sortSelector));

            if (index < 0) {
                throw new ProgrammerError("actually this is impossible...");
            }

            while (this.sortedComicProperties[index].Name != name) {
                index += 1;
            }

            return index;
        }

        private void AddComic(Comic comic, bool supressEvents = false) {
            // ToHashSet isn't a thing in .net standard 2.0...
            var properties = new HashSet<string>(this.getProperties(comic));

            foreach (var property in properties) {
                if (this.ContainsProperty(property)) {
                    this.accessor[property].Add(comic);
                    /* note: if the sortSelector is ItemCount, adding or removing an item will actually change an item's sort order! 
                     * we pretend it doesn't happen, but in the future we can choose to reload the item. */
                    if (!supressEvents) {
                        this.ComicPropertiesChanged?.Invoke(this,
                            new ComicPropertiesChangedEventArgs(ComicPropertyChangeType.ItemsChanged, property, added: new[] { comic }));
                    }
                } else {
                    this.AddProperty(property, new List<Comic> { comic });
                    // we need to create a new list, not use the one passed to AddProperty, since AddProperty consumes the list.
                    if (!supressEvents) {
                        this.ComicPropertiesChanged?.Invoke(this,
                        new ComicPropertiesChangedEventArgs(ComicPropertyChangeType.Added, property, added: new[] { comic }));
                    }
                }
            }

            this.savedProperties[comic.UniqueIdentifier] = properties;
        }

        private void RemoveComic(Comic comic, bool supressEvents = false) {
            foreach (var property in this.savedProperties[comic.UniqueIdentifier]) {
                var comics = this.accessor[property];
                _ = comics.Remove(comic);

                if (comics.Count == 0) {
                    this.RemoveProperty(property);
                    if (!supressEvents) {
                        this.ComicPropertiesChanged?.Invoke(this,
                        new ComicPropertiesChangedEventArgs(ComicPropertyChangeType.Removed, property, removed: new[] { comic }));
                    }
                } else {
                    if (!supressEvents) {
                        this.ComicPropertiesChanged?.Invoke(this,
                        new ComicPropertiesChangedEventArgs(ComicPropertyChangeType.ItemsChanged, property, removed: new[] { comic }));
                    }
                }
            }

            _ = this.savedProperties.Remove(comic.UniqueIdentifier);
        }

        private void Parent_ViewChanged(ComicView sender, ComicView.ViewChangedEventArgs e) {
            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                    foreach (var comic in e.Remove) {
                        this.RemoveComic(comic);
                    }

                    foreach (var comic in e.Add) {
                        this.AddComic(comic);
                    }

                    break;

                case ComicChangeType.ThumbnailChanged:
                    /* TODO: we don't actually have a route to notify a nav item of a thumbnail change */
                    break;

                case ComicChangeType.Refresh:
                    this.sortedComicProperties.Clear();
                    this.accessor.Clear();

                    foreach (var comic in sender) {
                        this.AddComic(comic, supressEvents: true);
                    }

                    this.ComicPropertiesChanged?.Invoke(this, new ComicPropertiesChangedEventArgs(ComicPropertyChangeType.Refresh));

                    break;

                default:
                    throw new ProgrammerError("unhandled switch case");
            }
        }

        //#region Dictionary-like behavior

        public ComicProperty this[string key] => new ComicProperty(key, this.accessor[key]);
        bool ContainsProperty(string property) => this.accessor.ContainsKey(property);

        public int Count => this.sortedComicProperties.Count;

        public IEnumerator<ComicProperty> GetEnumerator() => this.sortedComicProperties.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        //#endregion

        /// <summary>
        /// The ComicPropertyChanged event allows external classes using a ComicPropertiesView to
        /// update their information about comics that have changed in this view.
        /// </summary> 
        public event ComicPropertiesChangedEventHandler? ComicPropertiesChanged;
        public delegate void ComicPropertiesChangedEventHandler(SortedComicPropertiesView sender, ComicPropertiesChangedEventArgs e);
    }

    public class ComicPropertiesChangedEventArgs {
        public readonly ComicPropertyChangeType Type;
        public readonly string? PropertyName;
        public readonly IEnumerable<Comic>? Added;
        public readonly IEnumerable<Comic>? Removed;
        public readonly int? PreviousPosition;
        public readonly int? CurrentPosition;

        public ComicPropertiesChangedEventArgs(ComicPropertyChangeType type, string? propertyName = null,
                IEnumerable<Comic>? added = null, IEnumerable<Comic>? removed = null,
                int? previousPosition = null, int? currentPosition = null) {
            this.Type = type;
            this.PropertyName = propertyName;
            this.Added = added;
            this.Removed = removed;
            this.PreviousPosition = previousPosition;
            this.CurrentPosition = currentPosition;
        }
    }

    public enum ComicPropertyChangeType {
        /// <summary>
        /// Represents a new property name that did not previously exist. Comics that should belong to the property are
        /// stored in <c>Added</c>.
        /// </summary>
        Added,

        /// <summary>
        /// Represents a property that no longer exists. Comics that used to belong to the property are stored in 
        /// <c>Removed</c>.
        /// </summary>
        Removed,

        /// <summary>
        /// Represents a property that has its contents changed. New comics are in <c>Added</c>, and removed comics in <c>removed</c>.
        /// </summary>
        ItemsChanged,

        /// <summary>
        /// Represents a property that has changed positions in the sorting order, because an item was added or removed
        /// just now.
        /// </summary>
        PositionChanged,

        /// <summary>
        /// Represents that this view has changed so much that it cannot hope to tell which items have been added,
        /// and which have been removed. Receivers should just reload everything from this view.
        /// </summary>
        Refresh
    }
    //public class ComicPropertyView : IReadOnlyDictionary<string, IReadOnlyList<Comic>> {
    //    private readonly Dictionary<string, List<Comic>> mapping = new Dictionary<string, List<Comic>>();
    //    private readonly Func<Comic, IEnumerable<string>> getProperties;

    //    internal ComicPropertyView(Func<Comic, IEnumerable<string>> getProperties, SortedComicView trackChangesFrom) {
    //        this.getProperties = getProperties;
    //        trackChangesFrom.ComicChanged += this.TrackChangesFrom_ComicChanged;
    //    }

    //    private void TrackChangesFrom_ComicChanged(SortedComicView sender, ComicChangedEventArgs args) {
    //        if (args.Type == ComicChangedType.Change || args.Type == ComicChangedType.Remove) {
    //            foreach (var property in this.getProperties(args.OldComic!)) {
    //                _ = this.mapping[property].Remove(args.OldComic!);
    //                if (this.mapping[property].Count == 0) {
    //                    _ = this.mapping.Remove(property);
    //                }
    //            }
    //        }

    //        if (args.Type == ComicChangedType.Change || args.Type == ComicChangedType.Remove) {
    //            foreach (var property in this.getProperties(args.NewComic!)) {
    //                if (this.mapping.ContainsKey(property)) {
    //                    this.mapping[property] = new List<Comic> { args.NewComic! };
    //                } else {
    //                    this.mapping[property].Add(args.NewComic!);
    //                }
    //            }
    //        }
    //    }

    //    #region IReadOnlyDictionary implementation 

    //    public IReadOnlyList<Comic> this[string key] => this.mapping[key];
    //    public IEnumerable<string> Keys => this.mapping.Keys;
    //    public IEnumerable<IReadOnlyList<Comic>> Values => this.mapping.Values;
    //    public int Count => this.mapping.Count;
    //    public bool ContainsKey(string key) => this.mapping.ContainsKey(key);

    //    public IEnumerator<KeyValuePair<string, IReadOnlyList<Comic>>> GetEnumerator() {
    //        foreach (var (key, value) in this.mapping) {
    //            yield return new KeyValuePair<string, IReadOnlyList<Comic>>(key, value);
    //        }
    //    }

    //    public bool TryGetValue(string key, out IReadOnlyList<Comic> value) => this.TryGetValue(key, out value);
    //    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    //    #endregion
    //}
}
