﻿using ComicsLibrary.Collections;
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

        protected ComicNavigationItemGridViewModel(IMainPageContent parent, MainViewModel appViewModel, ComicCollectionView comicCollections)
            : base(parent, appViewModel)
        {
            this.collections = comicCollections;
            this.collections.CollectionsChanged += this.Collections_CollectionsChanged;
        }
        
        public static ComicNavigationItemGridViewModel ForViewModel(
            IMainPageContent parent, 
            MainViewModel mainViewModel, 
            ComicCollectionView comicCollections, 
            ComicItemGridState? savedState = null
        ) {
            var viewModel = new ComicNavigationItemGridViewModel(parent, mainViewModel, comicCollections);

            if (savedState is not null) {
                viewModel.SetComicItems(savedState.Items);
                viewModel.RequestedInitialScrollOffset = savedState.ScrollOffset;
            } else {
                // Sorts and loads the actual comic items
                viewModel.RefreshComicItems();
            }

            return viewModel;
        }

        protected void RefreshComicItems() {
            var items = this.collections.Select(collection =>
                new ComicNavigationItem(collection.Name, collection.Comics)
            ).ToList();

            this.SetComicItems(items);
        }

        private protected override void SortOrderChanged() {
            this.collections.SetSort(this.SelectedSortSelector);
            this.RefreshComicItems();
        }

        public void NavigateIntoItem(ComicNavigationItem item) {
            this.MainViewModel.NavigateInto(item);
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
                        var addedItems = e.Added.Select(name => new ComicNavigationItem(name, sender.GetView(name)));

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

        public override void Dispose() {
            base.Dispose();

            this.collections.CollectionsChanged -= this.Collections_CollectionsChanged;
        }
    }
}
