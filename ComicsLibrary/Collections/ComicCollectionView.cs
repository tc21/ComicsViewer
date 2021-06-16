using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ComicsLibrary.Sorting;

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
        public int Count => this.Properties.Count;
        public IEnumerator<IComicCollection> GetEnumerator() => this.Properties.GetEnumerator();
        public int? IndexOf(string name) => this.Properties.IndexOf(name);

        internal SortedComicCollections Properties { get; set; } = new(default, Array.Empty<IComicCollection>());

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public ComicCollectionSortSelector Sort { get; private set; }

        public void SetSort(ComicCollectionSortSelector sortSelector) {
            if (this.Sort == sortSelector) {
                return;
            }

            this.Sort = sortSelector;

            var previous = this.Properties.ToList();
            this.Properties = new(sortSelector, previous);

            previous.Clear();

            this.OnCollectionsChanged(new(CollectionsChangeType.Refresh));
        }

        public ComicView GetView(string name) => this.Properties.Get(name).Comics;

        /// <summary>
        /// The PropertiesChanged event is propagated down to children views so that external classes using a ComicPropertiesView can
        /// update their information about properties that have changed in this view.
        /// </summary> 
        public event CollectionsChangedEventHandler? CollectionsChanged;
        public delegate void CollectionsChangedEventHandler(ComicCollectionView sender, CollectionsChangedEventArgs e);

        protected void OnCollectionsChanged(CollectionsChangedEventArgs e) {
            this.CollectionsChanged?.Invoke(this, e);
        }

        public void AbandonChildren() {
            this.CollectionsChanged = null;
        }

        public abstract void DetachFromParent();
    }

    public class CollectionsChangedEventArgs {
        public readonly CollectionsChangeType Type;
        public readonly IEnumerable<string> Added;
        public readonly IEnumerable<string> Removed;

        internal CollectionsChangedEventArgs(CollectionsChangeType type, IEnumerable<string>? added = null, IEnumerable<string>? removed = null) {
            this.Type = type;
            this.Added = added ?? Array.Empty<string>();
            this.Removed = removed ?? Array.Empty<string>();
        }
    }

    public enum CollectionsChangeType {
        ItemsChanged, Refresh
    }
}
