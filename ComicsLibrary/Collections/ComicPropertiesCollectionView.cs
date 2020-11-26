using ComicsViewer.Common;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsLibrary.Collections {
    public class ComicPropertiesCollectionView : ComicCollectionView {
        private readonly ComicView parent;
        private readonly Func<Comic, IEnumerable<string>> getProperties;

        private readonly SortedComicCollections properties;

        public override int Count => this.properties.Count;

        public ComicPropertiesCollectionView(ComicView parent, Func<Comic, IEnumerable<string>> getProperties) {
            this.parent = parent;
            this.getProperties = getProperties;
            this.properties = new SortedComicCollections(this.Sort);

            parent.ViewChanged += this.ParentComicView_ViewChanged;

            this.InitializeProperties();
        }

        protected override void SortChanged() {
            this.properties.Clear();
            this.InitializeProperties();
        }

        private void ParentComicView_ViewChanged(ComicView sender, ComicView.ViewChangedEventArgs e) {
            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                    var addedProperties = new HashSet<string>();
                    var modifiedProperties = new HashSet<string>();
                    var removedProperties = new HashSet<string>();

                    var affectedProperties = new HashSet<string>(e.Remove.Concat(e.Add).SelectMany(this.getProperties));
                    foreach (var property in affectedProperties) {
                        if (this.properties.Contains(property)) {
                            var propertyView = this.properties.Remove(property);

                            if (propertyView.Comics.Any()) {
                                this.properties.Add(propertyView);

                                _ = modifiedProperties.Add(property);
                            } else {
                                _ = removedProperties.Add(property);
                            }
                        } else {
                            var view = this.parent.Filtered(comic => getProperties(comic).Contains(property));
                            this.properties.Add(new ComicCollection(property, view));

                            _ = addedProperties.Add(property);
                        }
                    }

                    this.OnCollectionsChanged(new(CollectionsChangeType.ItemsChanged, addedProperties, modifiedProperties, removedProperties));

                    break;
                case ComicChangeType.ThumbnailChanged:
                    break;
                case ComicChangeType.Refresh:
                    this.properties.Clear();
                    this.InitializeProperties();

                    break;

                default:
                    throw new ProgrammerError("unhandled switch case");
            }
        }

        private void InitializeProperties() {
            var propertyNames = new HashSet<string>();

            foreach (var comic in this.parent) {
                propertyNames.UnionWith(getProperties(comic));
            }

            foreach (var propertyName in propertyNames) {
                var view = this.parent.Filtered(comic => getProperties(comic).Contains(propertyName));
                this.properties.Add(new ComicCollection(propertyName, view));
            }

            this.OnCollectionsChanged(new CollectionsChangedEventArgs(CollectionsChangeType.Refresh, this.Select(p => p.Name)));
        }

        public override IEnumerator<IComicCollection> GetEnumerator() {
            return this.properties.GetEnumerator();
        }
    }
}
