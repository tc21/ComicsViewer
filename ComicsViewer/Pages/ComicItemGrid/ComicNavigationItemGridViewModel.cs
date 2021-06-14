using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using ComicsViewer.Support;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicNavigationItemGridViewModel : ComicItemGridViewModel {
        public override string[] SortSelectors => SortSelectorNames.ComicCollectionSortSelectorNames;
        protected ComicCollectionSortSelector SelectedSortSelector => (ComicCollectionSortSelector)this.SelectedSortIndex;

        private readonly ComicCollectionView collections;

        protected ComicNavigationItemGridViewModel(
            IMainPageContent parent, 
            MainViewModel appViewModel,
            NavigationTag navigationTag,
            ComicCollectionSortSelector initialSort
        ) : base(parent, appViewModel) {
            // Note: in the future, we may want to store this to remove the event handler
            switch (navigationTag) {
                case NavigationTag.Comics:
                    throw new ProgrammerError("Cannot make of navigation item view model for work items");

                case NavigationTag.Playlist:
                    this.collections = this.MainViewModel.Playlists;
                    this.collections.SetSort(initialSort);
                    break;

                default:
                    this.collections = this.MainViewModel.Comics.SortedProperties(navigationTag, initialSort);
                    break;
            }

            this.collections.CollectionsChanged += this.Collections_CollectionsChanged;
        }
        
        public static ComicNavigationItemGridViewModel ForViewModel(
            IMainPageContent parent, 
            MainViewModel mainViewModel,
            NavigationTag navigationTag,
            ComicCollectionSortSelector initialSort,
            ComicItemGridState? savedState = null
        ) {
            var viewModel = new ComicNavigationItemGridViewModel(parent, mainViewModel, navigationTag, initialSort);

            if (savedState?.LastModified is { } lastModified && lastModified == mainViewModel.LastModified) {
                viewModel.SetComicItems(savedState.Items);
            } else {
                viewModel.RefreshComicItems();
            }

            if (savedState?.ScrollOffset is { } offset) {
                viewModel.RequestedInitialScrollOffset = offset;
            }

            return viewModel;
        }

        protected void RefreshComicItems() {
            var items = this.MainViewModel.Comics.GetNavigationItems(this.NavigationTag, this.SelectedSortSelector);
            this.SetComicItems(items);
        }

        public override void SortAndRefreshComicItems() {
            this.RefreshComicItems();
        }

        private void Collections_CollectionsChanged(ComicCollectionView sender, CollectionsChangedEventArgs e) {
            switch (e.Type) {
                case CollectionsChangeType.ItemsChanged:
                    if (e.Removed.Any()) {
                        var removedTitles = e.Removed.ToHashSet();
                        var removeIndices = new List<int>();

                        var index = 0;
                        foreach (var item in this.ComicItems) {
                            if (removedTitles.Contains(item.Title)) {
                                removeIndices.Insert(0, index);
                            }

                            index += 1;
                        };

                        foreach (var i in removeIndices) {
                            this.ComicItems.RemoveAt(i);
                        }
                    }

                    if (e.Added.Any()) {
                        var addedItems = e.Added.Select(name => this.MainViewModel.Comics.GetNavigationItem(this.NavigationTag, name));

                        foreach (var item in addedItems) {
                            this.ComicItems.Insert(0, item);
                        }
                    }

                    break;

                case CollectionsChangeType.Refresh:
                    this.RefreshComicItems();
                    break;

                default:
                    throw new ProgrammerError($"{nameof(ComicNavigationItemGridViewModel)}.{nameof(this.Collections_CollectionsChanged)}: unhandled switch case");
            }
        }

        public override void RemoveEventHandlers() {
            base.RemoveEventHandlers();

            this.collections.CollectionsChanged -= this.Collections_CollectionsChanged;
        }
    }
}
