using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.ViewModels;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.Support {
    public class MainComicList : ComicList {
        public readonly Filter Filter = new();

        public MainComicList() {
            this.Filter.FilterChanged += this.Filter_FilterChanged;
        }

        // another method purely for the purpose of UI
        public void NotifyThumbnailChanged(IEnumerable<Comic> comics) {
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ThumbnailChanged, comics));
        }

        private ComicView? _filtered;
        public ComicView Filtered() {
            this._filtered ??= this.Filtered(this.Filter.ShouldBeVisible);
            return this._filtered;
        }

        private void Filter_FilterChanged(Filter filter) {
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.Refresh));
        }

        protected override void RefreshComics(IEnumerable<Comic> comics) {
            base.RefreshComics(comics);

            this._filtered = null;
            this.InvalidateCache();
        }

        protected override void RemoveComic(Comic comic) {
            base.RemoveComic(comic);

            _ = comicWorkItems.Remove(comic.UniqueIdentifier);
        }

        protected override void AddComic(Comic comic) {
            base.AddComic(comic);

            // Adding any item will cause our cache to need to be updated.
            // Considering adding items is a rarer occurence than navigation, we'll just invalidate the entire cache
            this.InvalidateCache();
        }

        // Note: we haven't tested if the cache is actually needed for performance, so maybe we should test it
        private readonly Dictionary<string, ComicWorkItem> comicWorkItems = new();
        private readonly Dictionary<NavigationTag, ComicPropertiesCollectionView> comicPropertyCollections = new();
        private readonly Dictionary<NavigationTag, Dictionary<string, ComicNavigationItem>> comicNavigationItems = new();

        private void InvalidateCache() {
            this.comicWorkItems.Clear();
            this.comicPropertyCollections.Clear();
            this.comicNavigationItems.Clear();
        }

        public ComicWorkItem GetWorkItem(Comic comic) {
            if (!this.comicWorkItems.TryGetValue(comic.UniqueIdentifier, out var item)) {
                item = new ComicWorkItem(comic, this);
                item.RequestingRefresh += this.ComicWorkItem_RequestingRefresh;
                this.comicWorkItems.Add(comic.UniqueIdentifier, item);
            }

            return item;
        }

        public ComicPropertiesCollectionView SortedProperties(NavigationTag navigationTag, ComicCollectionSortSelector? sortSelector) {
            if (!this.comicPropertyCollections.TryGetValue(navigationTag, out var view)) {
                view = this.SortedProperties(
                    navigationTag switch {
                        NavigationTag.Author => comic => new[] { comic.Author },
                        NavigationTag.Category => comic => new[] { comic.Category },
                        NavigationTag.Tags => comic => comic.Tags,
                        _ => throw new ProgrammerError($"unsupported navigation tag ({navigationTag})")
                    },
                    sortSelector ?? default
                );

                this.comicPropertyCollections.Add(navigationTag, view);
            } else if (sortSelector is { } selector) {
                view.SetSort(selector);
            }

            return view;
        }

        public List<ComicNavigationItem> GetNavigationItems(NavigationTag tag, ComicCollectionSortSelector? sortSelector = null) {
            // TODO: playlists aren't supported yet
            var collectionView = this.SortedProperties(tag, sortSelector);
            return collectionView.Select(collection => this.GetOrMakeNavigationItem(tag, collection.Name, collection.Comics))
                .ToList();
        }

        public ComicNavigationItem GetNavigationItem(NavigationTag tag, string name) {
            // TODO: playlists aren't supported yet
            var collectionView = this.SortedProperties(tag, null);

            return this.GetOrMakeNavigationItem(tag, name, collectionView.GetView(name));
        }

        private ComicNavigationItem GetOrMakeNavigationItem(NavigationTag tag, string name, ComicView comics) {
            if (!this.comicNavigationItems.TryGetValue(tag, out var items)) {
                items = new();
                this.comicNavigationItems.Add(tag, items);
            }

            if (!items.TryGetValue(name, out var item)) {
                item = new ComicNavigationItem(name, comics);
                items.Add(name, item);
            }

            return item;
        }

        private void ComicWorkItem_RequestingRefresh(ComicWorkItem sender, ComicWorkItem.RequestingRefreshType type) {
            switch (type) {   // switch RequestingRefreshType
                //case ComicWorkItem.RequestingRefreshType.Reload:
                case ComicWorkItem.RequestingRefreshType.Remove:
                    _ = this.comicWorkItems.Remove(sender.Comic.UniqueIdentifier);
                    break;

                default:
                    throw new ProgrammerError("Unhandled switch case");
            }
        }
    }
}
