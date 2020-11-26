using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsLibrary.Collections {
    public class AggregateCollectionView : ComicCollectionView {
        private readonly SortedComicCollections properties;

        public override int Count => this.properties.Count;
        private readonly Dictionary<string, (IComicCollection collection, ComicView.ComicsChangedEventHandler handler)> collections = new();

        public AggregateCollectionView(IEnumerable<IComicCollection> collections) {
            this.properties = new SortedComicCollections(this.Sort);

            foreach (var collection in collections) {
                void handler(ComicView sender, ComicsChangedEventArgs e) => this.Collection_ComicsChanged(collection.Name, sender, e);

                this.properties.Add(new ComicCollection(collection.Name, collection.Comics));
                this.collections.Add(collection.Name, (collection, (ComicView.ComicsChangedEventHandler)handler));
                collection.Comics.ComicsChanged += handler;
            }
        }

        public void AddCollection(IComicCollection collection) {
            void handler(ComicView sender, ComicsChangedEventArgs e) => this.Collection_ComicsChanged(collection.Name, sender, e);

            this.properties.Add(new ComicCollection(collection.Name, collection.Comics));
            this.collections.Add(collection.Name, (collection, (ComicView.ComicsChangedEventHandler)handler));
            collection.Comics.ComicsChanged += handler;

            this.OnCollectionsChanged(new(CollectionsChangeType.ItemsChanged, added: new[] { collection.Name }));
        }

        public void RemoveCollection(IComicCollection collection) {
            var (existing, handler) = this.collections[collection.Name];
            _ = this.collections.Remove(collection.Name);
            _ = this.properties.Remove(collection.Name);

            existing.Comics.ComicsChanged -= handler;

            this.OnCollectionsChanged(new(CollectionsChangeType.ItemsChanged, removed: new[] { collection.Name }));
        }

        protected override void SortChanged() {
            this.properties.Clear();

            foreach(var (collection, _) in this.collections.Values) {
                this.properties.Add(new ComicCollection(collection.Name, collection.Comics));
            }
        }

        private void Collection_ComicsChanged(string collectionName, ComicView sender, ComicsChangedEventArgs e) {
            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                case ComicChangeType.Refresh:
                    bool removed = false, added = false;

                    if (this.properties.Contains(collectionName)) {
                        _ = this.properties.Remove(collectionName);
                        removed = true;
                    }

                    if (sender.Any()) {
                        this.properties.Add(new ComicCollection(collectionName, sender));
                        added = true;
                    }

                    var names = new[] { collectionName };
                    CollectionsChangedEventArgs notification;

                    if (removed && added) {
                        notification = new(CollectionsChangeType.ItemsChanged, modified: names);
                    } else if (removed) {
                        notification = new(CollectionsChangeType.ItemsChanged, removed: names);
                    } else if (added) {
                        notification = new(CollectionsChangeType.ItemsChanged, added: names);
                    } else {
                        break;
                    }

                    this.OnCollectionsChanged(notification);

                    break;

                case ComicChangeType.ThumbnailChanged:
                    break;

                default:
                    break;
            }
        }

        public override IEnumerator<ComicCollection> GetEnumerator() {
            return this.properties.GetEnumerator();
        }
    }
}
