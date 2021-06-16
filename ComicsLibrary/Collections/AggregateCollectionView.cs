using System.Collections.Generic;
using System.Linq;

namespace ComicsLibrary.Collections {
    public class AggregateCollectionView : ComicCollectionView {
        private readonly Dictionary<string, (IComicCollection collection, ComicView.ComicsChangedEventHandler handler)> collections = new();

        public void AddCollection(IComicCollection collection) {
            void handler(ComicView sender, ComicsChangedEventArgs e) => this.Collection_ComicsChanged(collection, e);

            this.Properties.Add(collection);
            this.collections.Add(collection.Name, (collection, (ComicView.ComicsChangedEventHandler)handler));
            collection.Comics.ComicsChanged += handler;

            this.OnCollectionsChanged(new(CollectionsChangeType.ItemsChanged, added: new[] { collection.Name }));
        }

        public void RemoveCollection(IComicCollection collection) {
            var (existing, handler) = this.collections[collection.Name];
            _ = this.collections.Remove(collection.Name);
            _ = this.Properties.Remove(collection.Name);

            existing.Comics.ComicsChanged -= handler;

            this.OnCollectionsChanged(new(CollectionsChangeType.ItemsChanged, removed: new[] { collection.Name }));
        }

        public IEnumerable<string> Keys => this.collections.Keys;

        public bool ContainsKey(string name) {
            return this.collections.ContainsKey(name);
        }

        public IComicCollection GetCollection(string name) {
            return this.collections[name].collection;
        }

        public void Clear() {
            foreach (var (existing, handler) in this.collections.Values) {
                existing.Comics.ComicsChanged -= handler;
            }

            this.collections.Clear();
            this.Properties.Clear();

            this.OnCollectionsChanged(new(CollectionsChangeType.Refresh));
        }

        private void Collection_ComicsChanged(IComicCollection collection, ComicsChangedEventArgs e) {
            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                case ComicChangeType.Refresh:
                    string[]? removed = null, added = null;

                    if (this.Properties.Contains(collection.Name)) {
                        _ = this.Properties.Remove(collection.Name);
                        removed = new[] { collection.Name };
                    }

                    if (collection.Comics.Any()) {
                        this.Properties.Add(collection);
                        added = new[] { collection.Name };
                    } else {
                        _ = this.collections.Remove(collection.Name);
                    }

                    this.OnCollectionsChanged(new(CollectionsChangeType.ItemsChanged, added, removed));

                    break;

                case ComicChangeType.ThumbnailChanged:
                    break;

                default:
                    break;
            }
        }

        public override void DetachFromParent() {
            foreach (var (collection, _) in this.collections.Values) {
                collection.Comics.DetachFromParent();
            }
        }
    }
}
