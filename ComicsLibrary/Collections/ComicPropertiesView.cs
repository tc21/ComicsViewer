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

        private readonly Func<Comic, IEnumerable<string>> getProperties;
        private ComicPropertySortSelector sortSelector;
        private bool sorted = false;

        public SortedComicPropertiesView(ComicView parent, Func<Comic, IEnumerable<string>> getProperties, ComicPropertySortSelector sortSelector) {
            this.parent = parent;
            this.getProperties = getProperties;
            this.sortSelector = sortSelector;

            foreach (var comic in parent) {
                this.AddComic(comic, preserveSort: false);
            }

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

            this.sorted = true;
        }

        // the caller must ensure this.sortSelector is not .Random
        private int GetIndexOfExistingPropertyNotRandomSortOrder(string name) {
            var property = this[name];
            var index = this.sortedComicProperties.BinarySearch(property, ComicPropertyComparers.Make(this.sortSelector));

            if (index < 0) {
                throw new ProgrammerError($"{nameof(GetIndexOfExistingPropertyNotRandomSortOrder)} retrieving a property that doesn't exist " +
                    $"(this will also fail if the list is no longer sorted)");
            }

            while (this.sortedComicProperties[index].Name != name) {
                index += 1;
            }

            return index;
        }

        private void AddComic(Comic comic, bool preserveSort = true) {
            // ToHashSet isn't a thing in .net standard 2.0...
            var properties = new HashSet<string>(this.getProperties(comic));

            foreach (var property in properties) {
                if (this.ContainsProperty(property)) {
                    this.AddComicToProperty(property, comic, preserveSort);
                } else {
                    this.AddProperty(property, new List<Comic> { comic }, preserveSort);
                }
            }

            if (!preserveSort) {
                this.sorted = false;
            }
        }

        private void RemoveComic(Comic comic) {
            if (!this.sorted) {
                throw new ProgrammerError($"{nameof(RemoveComic)} should not be called when the list is not guaranteed to be sorted." +
                    $"(this is not a constraint but a design decision)");
            }

            foreach (var property in this.getProperties(comic)) {
                // RemoveComicFromProperty will call RemoveProperty if the property becomes empty
                this.RemoveComicFromProperty(property, comic);
            }
        }

        /// <summary>
        /// This function handles property adding, including for situations that might influence an item's sort order.
        /// We don't need any info about how a property moved, but it might help in the future: 
        /// for example, we might have RemoveProperty return an item's previous position, and AddProperty return and item's new position.
        /// 
        /// This function is designed to only be called from <see cref="AddComic"/>, as such, calling it from elsewhere will result in incorrect 
        /// behavior when called in a certain way. Specifically, constraints that an item must not already exist, and
        /// setting the sorting flag only happens there.
        /// </summary>
        private void AddComicToProperty(string property, Comic comic, bool preserveSort = true) {
            /* Note on this implementation of AddComicToProperty:
             * currently, there is no method of relaying any addition, removal, or otherwise change of a property to a ComicItemGridViewController.
             * The current behavior is to throw away this ComicPropertiesView, and create a new one. In other words, we don't even have to bothre handling
             * Adding an item while preserving the list's sort order: It's sorted, used once, and thrown away. When we implement events for 
             * ComicPropertyViews, this behavior will become relevant again. I have left this comment as a note, so that it exists in the repo
             * before I delete this part of the code, so that it serves as a guide when we decide to reimplement this feature. 
             * 
             * I will also note here that this is why navigating into a nav view item, editing something, then navigating out only preserves 
             * navigation location for the first 100 shown nav view items. */
            if (preserveSort && this.ModifyingPropertyComicsMayChangeSortOrder) {
                var comics = this.accessor[property];
                this.RemoveProperty(property);
                comics.Add(comic);
                this.AddProperty(property, comics);
            } else {
                this.accessor[property].Add(comic);
            }
        }

        /// <summary>
        /// Just like the function above, calling this function from outside of <see cref="RemoveComic"/> will result in 
        /// incorrect application state if you pass specific parameters that do so.
        /// 
        /// Specifically, constraints such at an item must exist, and the list must ensure it is currently sorted,
        /// are only checked in RemoveComic.
        /// </summary>
        private void RemoveComicFromProperty(string property, Comic comic) {
            var comics = this.accessor[property];

            if (this.ModifyingPropertyComicsMayChangeSortOrder) {
                this.RemoveProperty(property);
                _ = comics.Remove(comic);
                if (comics.Count > 0) {
                    this.AddProperty(property, comics);
                }
            } else {
                _ = comics.Remove(comic);
                if (comics.Count == 0) {
                    this.RemoveProperty(property);
                }
            }
        }

        /// <summary>
        /// Do not call this method. See <see cref="AddComicToProperty"/> for why.
        /// </summary>
        private void AddProperty(string name, List<Comic> comics, bool preserveSort = true) {
            if (this.ContainsProperty(name)) {
                throw new ProgrammerError("comic already exists in this collection");
            }

            var property = new ComicProperty(name, comics);

            if (!preserveSort) {
                this.sortedComicProperties.Add(property);
            } else {
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
            }

            this.accessor[property.Name] = comics;
        }
        
        /// <summary>
        /// Do not call this method. See <see cref="RemoveComicFromProperty"/> for why.
        /// </summary>
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

        private bool ModifyingPropertyComicsMayChangeSortOrder => this.sortSelector switch {
            ComicPropertySortSelector.Name => false,
            ComicPropertySortSelector.ItemCount => true,
            ComicPropertySortSelector.Random => false,
            _ => throw new ProgrammerError("unhandled switch case")
        };

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
