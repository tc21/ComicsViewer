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
        public IEnumerable<Comic> Comics { get; }

        public ComicProperty(string name, IEnumerable<Comic> comics) {
            this.Comics = comics.ToList();
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
    public class OneTimeComicPropertiesView : IReadOnlyCollection<ComicProperty> {
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

        public OneTimeComicPropertiesView(ComicView parent, Func<Comic, IEnumerable<string>> getProperties, ComicPropertySortSelector sortSelector) {
            this.parent = parent;
            this.getProperties = getProperties;

            foreach (var comic in parent) {
                this.AddComic(comic);
            }

            this.Sort(sortSelector);
        }

        public ComicView PropertyView(string property) {
            // used to throw an ArgumentException if property doesn't exist
            _ = this[property];

            return this.parent.Filtered(c => this.getProperties(c).Contains(property));
        }

        /// <summary>
        /// Sorts this view in-place.
        /// </summary>
        private void Sort(ComicPropertySortSelector sortSelector) {
            if (sortSelector == ComicPropertySortSelector.Random) {
                General.Shuffle(this.sortedComicProperties);
            } else {
                this.sortedComicProperties.Sort(ComicPropertyComparers.Make(sortSelector));
            }
        }

        /// <summary>
        /// Adds a comic. Sorting is not preserced, and you must call Sort() again manually to ensure
        /// the view stays sorted.
        /// </summary>
        private void AddComic(Comic comic) {
            // ToHashSet isn't a thing in .net standard 2.0...
            foreach (var property in this.getProperties(comic)) {
                if (this.ContainsProperty(property)) {
                    this.accessor[property].Add(comic);
                } else {
                    this.AddProperty(property, new List<Comic> { comic });
                }
            }
        }

        /// <summary>
        /// Adds a property. Sorting is not preserced, and you must call Sort() again manually to ensure
        /// the view stays sorted.
        /// </summary>
        private void AddProperty(string name, List<Comic> comics) {
            if (this.ContainsProperty(name)) {
                throw new ProgrammerError("comic already exists in this collection");
            }

            var property = new ComicProperty(name, comics);
            this.sortedComicProperties.Add(property);
            this.accessor[property.Name] = comics;
        }

        private ComicProperty this[string key] => new ComicProperty(key, this.accessor[key]);
        private bool ContainsProperty(string property) => this.accessor.ContainsKey(property);

        public int Count => this.sortedComicProperties.Count;

        public IEnumerator<ComicProperty> GetEnumerator() => this.sortedComicProperties.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

}
