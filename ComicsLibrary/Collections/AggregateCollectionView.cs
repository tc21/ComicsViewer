﻿using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsLibrary.Collections {
    public class AggregateCollectionView : ComicCollectionView {
        public override int Count => this.Properties.Count;
        private readonly Dictionary<string, (IComicCollection collection, ComicView.ComicsChangedEventHandler handler)> collections = new();

        public AggregateCollectionView() : this(Array.Empty<IComicCollection>()) {}

        public AggregateCollectionView(IEnumerable<IComicCollection> collections) {
            foreach (var collection in collections) {
                void handler(ComicView sender, ComicsChangedEventArgs e) => this.Collection_ComicsChanged(collection, e);

                this.collections.Add(collection.Name, (collection, (ComicView.ComicsChangedEventHandler)handler));
                collection.Comics.ComicsChanged += handler;
            }

            this.Properties = new(this.Sort, collections);
        }

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
                    bool removed = false, added = false;

                    if (this.Properties.Contains(collection.Name)) {
                        _ = this.Properties.Remove(collection.Name);
                        removed = true;
                    }

                    if (collection.Comics.Any()) {
                        this.Properties.Add(collection);
                        added = true;
                    }

                    var names = new[] { collection.Name };
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

        public override IEnumerator<IComicCollection> GetEnumerator() {
            return this.Properties.GetEnumerator();
        }
    }
}
