using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ComicsViewer.Common;

#nullable enable

namespace ComicsLibrary.Collections {
    /// <summary>
    /// A generic collection of comics that can be used to search and sort a list of comics
    /// ComicViews are immutable, but they might have filters, and sorts placed on them that can be used to dynamically
    /// change their contents. Any changes will be signaled by one or more <see cref="ComicsChanged"/> events.
    /// </summary>
    public abstract class ComicView : IEnumerable<Comic> {
        public static readonly ComicView Empty = new ComicList();

        // comics should be considered equivalent if their UniqueIdentifier is the same.
        public bool Contains(Comic comic) => this.Contains(comic.UniqueIdentifier);
        public Comic GetStored(Comic comic) => this.GetStored(comic.UniqueIdentifier);
        public abstract bool Contains(string uniqueIdentifier);
        public abstract Comic GetStored(string uniqueIdentifier);
        public abstract IEnumerator<Comic> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public virtual int Count() {
            return ((IEnumerable<Comic>)this).Count();
        }

        private protected ComicView(ComicView? trackChangesFrom = null) {
            if (trackChangesFrom != null) {
                trackChangesFrom.ViewChanged += this.ParentComicView_ViewChanged;
            }
        }

        private protected virtual void ParentComicView_ViewChanged(ComicView sender, ViewChangedEventArgs e) {
            this.OnComicChanged(e);
        }

        public virtual SortedComicView Sorted(Sorting.ComicSortSelector sortSelector)
            => new SortedComicView(this, comics: this, sortSelector);


        public FilteredComicView Filtered(Func<Comic, bool> filter)
            => new FilteredComicView(this, filter);

        public OneTimeComicPropertiesView SortedProperties(
            Func<Comic, IEnumerable<string>> getProperties, Sorting.ComicPropertySortSelector sortSelector
        ) {
            return new OneTimeComicPropertiesView(this, getProperties, sortSelector);
        }

        /// <summary>
        /// The ViewChanged event is propagated down to children views so they can update their information if needed.
        /// Child classes can override ParentComicView_ViewChanged to provide custom behavior.
        /// Child classes can make themselves mutable, and call OnComicChanged to notify any changes to their comics.
        /// Overriding classes are required to ensure their implementation propagates events only for comics that need
        /// to be added or removed
        /// </summary>
        protected internal event ViewChangedEventHandler? ViewChanged;
        protected internal delegate void ViewChangedEventHandler(ComicView sender, ViewChangedEventArgs e);
        protected void OnComicChanged(ViewChangedEventArgs e) {
            this.ViewChanged?.Invoke(this, e);
            this.ComicsChanged?.Invoke(this, e.ToComicsChangedEventArgs());
        }

        protected internal class ViewChangedEventArgs {
            public readonly ComicChangeType Type;
            public readonly IReadOnlyList<Comic> Add;
            public readonly IReadOnlyList<Comic> Remove;

            public ViewChangedEventArgs(ComicChangeType type, IEnumerable<Comic>? add = null, IEnumerable<Comic>? remove = null) {
                this.Type = type;
                this.Add = add?.ToList() ?? new List<Comic>();
                this.Remove = remove?.ToList() ?? new List<Comic>();
            }

            public ComicsChangedEventArgs ToComicsChangedEventArgs() {
                switch (this.Type) {  // switch ChangeType
                    case ComicChangeType.ItemsChanged:
                        var removed = this.Remove.ToDictionary(c => c.UniqueIdentifier);

                        var modified = new List<Comic>();
                        var added = new List<Comic>();

                        foreach (var comic in this.Add) {
                            if (removed.ContainsKey(comic.UniqueIdentifier)) {
                                _ = removed.Remove(comic.UniqueIdentifier);
                                modified.Add(comic);
                            } else {
                                added.Add(comic);
                            }
                        }

                        return new ComicsChangedEventArgs(this.Type, new ComicList(added), new ComicList(modified), new ComicList(removed.Values));

                    case ComicChangeType.Refresh:
                        return new ComicsChangedEventArgs(this.Type);

                    case ComicChangeType.ThumbnailChanged:
                        return new ComicsChangedEventArgs(this.Type, modified: new ComicList(this.Add));

                    default:
                        throw new ProgrammerError($"{nameof(ViewChangedEventArgs)}.{nameof(this.ToComicsChangedEventArgs)}: unhandled switch case");
                }
            }
        }

        /// <summary>
        /// The ComicsChanged event is propagated down to children views so that external classes using a ComicView can
        /// update their information about comics that have changed in this view.
        /// </summary> 
        public event ComicsChangedEventHandler? ComicsChanged;
        public delegate void ComicsChangedEventHandler(ComicView sender, ComicsChangedEventArgs e);
    }

    public class ComicsChangedEventArgs {
        public readonly ComicChangeType Type;
        public readonly ComicView Added;
        public readonly ComicView Modified;
        public readonly ComicView Removed;

        internal ComicsChangedEventArgs(ComicChangeType type, ComicView? added = null,
                                      ComicView? modified = null, ComicView? removed = null) {
            this.Type = type;
            this.Added = added ?? ComicView.Empty;
            this.Modified = modified ?? ComicView.Empty;
            this.Removed = removed ?? ComicView.Empty;
        }
    }

    public enum ComicChangeType {
        /// <summary>
        /// <list type="table">
        /// 
        /// <item>Represents that one or more items has changed. <br/></item>
        /// 
        /// <item>In a <see cref="ComicView.ViewChangedEventArgs"/>: <c>Add</c> and <c>Remove</c> have been populated with items that have been 
        /// added to and removed from the sender. Child views should add/remove, and propagate, where appropriate.</item>
        /// 
        /// <item>In a <see cref="ComicsChangedEventArgs"/>: By design, all items in <c>Removed</c> and <c>Modified</c> previously
        /// existed, while none in <c>Added</c> did. All items in <c>Added</c> and <c>Modified</c> currently exist.</item>
        /// 
        /// </list>
        /// 
        /// <remarks>
        /// Note: the only information that is guaranteed to be accurate for modified items is a comic's hard-coded title, 
        /// author, category, and UniqueIdentifier.
        /// Do not rely on a modified comic's metadata to be from before it was modified. Most likely, it contains the new data
        /// that has already been modified.
        /// </remarks>
        /// </summary>
        ItemsChanged,

        /// <summary>
        /// <list type="table">
        /// <item>Only sent by external requests. Nothing about the underlying comics of this view has changed, but the
        /// thumbnails for the items in has changed.</item>
        /// <item>In a <see cref="ComicView.ViewChangedEventArgs"/>: items are stored in <see cref="ComicView.ViewChangedEventArgs.Add"/>.</item>
        /// <item>In a <see cref="ComicsChangedEventArgs"/>: items are stored in <see cref="ComicsChangedEventArgs.Modified"/>.</item>
        /// </list>
        /// </summary>
        ThumbnailChanged,

        /// <summary>
        /// Represents that this view has changed so much that it cannot hope to tell which items have been added,
        /// and which have been removed. Receivers should just reload everything from this view.
        /// </summary>
        Refresh
    }
}
