﻿using ComicsLibrary.Sorting;
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

            /* If you refer to the comments by AddComic, Adding a comic actually invalidates a sort!
             * So we sort again. It doubles our runtime, but the runtime of sorting anywhere between
             * 4 to 1000 items (which is the scale we're at) is negligible (and doubling doesn't change complexity). */
            this.Sort(this.sortSelector);

            parent.ViewChanged += this.Parent_ViewChanged;
        }

        public ComicView PropertyView(string property) {
            // used to throw an ArgumentException if property doesn't exist
            _ = this[property];

            return this.parent.Filtered(c => this.getProperties(c).Contains(property));
        }

        /// <summary>
        /// Sorts this view in-place.
        /// </summary>
        public void Sort(ComicPropertySortSelector sortSelector) {
            this.sortSelector = sortSelector;

            if (sortSelector == ComicPropertySortSelector.Random) {
                General.Shuffle(this.sortedComicProperties);
            } else {
                this.sortedComicProperties.Sort(ComicPropertyComparers.Make(sortSelector));
            }
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

        private void AddComic(Comic comic) {
            // ToHashSet isn't a thing in .net standard 2.0...
            var properties = new HashSet<string>(this.getProperties(comic));

            foreach (var property in properties) {
                if (this.ContainsProperty(property)) {
                    this.accessor[property].Add(comic);
                    /* note: if the sortSelector is ItemCount, adding or removing an item will actually change an item's sort order! 
                     * we pretend it doesn't happen, but in the future we can choose to reload the item. */
                } else {
                    this.AddProperty(property, new List<Comic> { comic });
                }
            }

            this.savedProperties[comic.UniqueIdentifier] = properties;
        }

        private void RemoveComic(Comic comic) {
            foreach (var property in this.savedProperties[comic.UniqueIdentifier]) {
                var comics = this.accessor[property];
                _ = comics.Remove(comic);

                if (comics.Count == 0) {
                    this.RemoveProperty(property);
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
                        this.AddComic(comic);
                    }

                    break;

                default:
                    throw new ProgrammerError("unhandled switch case");
            }
        }

        public ComicProperty this[string key] => new ComicProperty(key, this.accessor[key]);
        bool ContainsProperty(string property) => this.accessor.ContainsKey(property);

        public int Count => this.sortedComicProperties.Count;

        public IEnumerator<ComicProperty> GetEnumerator() => this.sortedComicProperties.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

}