using ComicsLibrary.Sorting;
using System;
using System.Collections;
using System.Collections.Generic;

#nullable enable

namespace ComicsLibrary.Collections {
    public interface IComicCollection {
        public string Name { get; }
        public ComicView Comics { get; }
    }

    public class ComicCollection : IComicCollection {
        public string Name { get; }
        public ComicView Comics { get; }

        public ComicCollection(string name, ComicView comics) {
            this.Comics = comics;
            this.Name = name;
        }
    }

    public abstract class ComicCollectionView : IReadOnlyCollection<IComicCollection> {
        public abstract int Count { get; }
        public abstract IEnumerator<IComicCollection> GetEnumerator();
        internal abstract SortedComicCollections Properties { get; }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public ComicCollectionSortSelector Sort { get; private set; }

        public void SetSort(ComicCollectionSortSelector sortSelector) {
            this.Sort = sortSelector;
            this.SortChanged();
        }

        public ComicView GetView(string name) => this.Properties.Get(name).Comics;

        protected abstract void SortChanged();

        /// <summary>
        /// The PropertiesChanged event is propagated down to children views so that external classes using a ComicPropertiesView can
        /// update their information about properties that have changed in this view.
        /// </summary> 
        public event CollectionsChangedEventHandler? CollectionsChanged;
        public delegate void CollectionsChangedEventHandler(ComicCollectionView sender, CollectionsChangedEventArgs e);

        protected void OnCollectionsChanged(CollectionsChangedEventArgs e) {
            this.CollectionsChanged?.Invoke(this, e);
        }
    }

    public class CollectionsChangedEventArgs {
        public readonly CollectionsChangeType Type;
        public readonly IEnumerable<string> Added;
        public readonly IEnumerable<string> Modified;
        public readonly IEnumerable<string> Removed;

        internal CollectionsChangedEventArgs(CollectionsChangeType type, IEnumerable<string>? added = null,
                                             IEnumerable<string>? modified = null, IEnumerable<string>? removed = null) {
            this.Type = type;
            this.Added = added ?? Array.Empty<string>();
            this.Modified = modified ?? Array.Empty<string>();
            this.Removed = removed ?? Array.Empty<string>();
        }
    }

    public enum CollectionsChangeType {
        ItemsChanged, Refresh
    }
}
