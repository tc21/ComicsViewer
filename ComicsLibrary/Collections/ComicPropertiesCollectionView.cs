using ComicsViewer.Common;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsLibrary.Collections {
    public class ComicPropertiesCollectionView : ComicCollectionView {
        private readonly ComicView parent;
        private readonly Func<Comic, IEnumerable<string>> getProperties;

        public override int Count => this.Properties.Count;

        public ComicPropertiesCollectionView(ComicView parent, Func<Comic, IEnumerable<string>> getProperties) {
            this.parent = parent;
            this.getProperties = getProperties;

            // note: because this.Properties[...].Comics are created after this, ViewChanged gets called for them
            // after it gets called for this. We have to monitor ComicsChanged for this.
            parent.ViewChanged += this.ParentComicView_ViewChanged;
            parent.ComicsChanged += this.ParentComicView_ComicsChanged;

            this.InitializeProperties();
        }

        private ComicView.ViewChangedEventArgs? lastChange;

        private void ParentComicView_ViewChanged(ComicView sender, ComicView.ViewChangedEventArgs e) {
            this.lastChange = e;
        }

        private void ParentComicView_ComicsChanged(ComicView sender, ComicsChangedEventArgs __) {
            if (this.lastChange is not { } e) {
                return;
            }
            
            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                    // here we get a list of added/removed/modified comics, and we need to create a list of
                    // added/removed/modified properties. 

                    // note: we can do some proper deduction in terms of which properties are added/removed/modified,
                    // but we're keeping it simple for now: basically, modified = removed + added
                    var tryAddProperties = e.Add.SelectMany(this.getProperties);
                    var tryRemoveProperties = e.Remove.SelectMany(this.getProperties);

                    var addedProperties = new HashSet<string>();
                    var removedProperties = new HashSet<string>();

                    foreach (var property in tryRemoveProperties) {
                        if (this.Properties.Contains(property)) {
                            var propertyView = this.Properties.Remove(property);
                            _ = removedProperties.Add(property);

                            if (propertyView.Comics.Any()) {
                                this.Properties.Add(propertyView);
                                _ = addedProperties.Add(property);
                            }
                        }
                    }

                    foreach (var property in tryAddProperties) {
                        if (this.Properties.Contains(property)) {
                            _ = removedProperties.Add(property);
                        } else {
                            var view = this.parent.Filtered(comic => getProperties(comic).Contains(property));
                            this.Properties.Add(new ComicCollection(property, view));
                        }

                        _ = addedProperties.Add(property);
                    }

                    this.OnCollectionsChanged(new(CollectionsChangeType.ItemsChanged, addedProperties, removedProperties));

                    break;
                case ComicChangeType.ThumbnailChanged:
                    break;
                case ComicChangeType.Refresh:
                    this.Properties.Clear();
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

            var collections = propertyNames.Select(name => {
                var view = this.parent.Filtered(comic => getProperties(comic).Contains(name));
                return new ComicCollection(name, view);
            });

            this.Properties = new(this.Sort, collections);

            this.OnCollectionsChanged(new CollectionsChangedEventArgs(CollectionsChangeType.Refresh, this.Select(p => p.Name)));
        }

        public override IEnumerator<IComicCollection> GetEnumerator() {
            return this.Properties.GetEnumerator();
        }
    }
}
