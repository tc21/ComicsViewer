using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using Microsoft.Toolkit.Uwp.UI.Controls.TextToolbarSymbols;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicNavigationItemGridViewModel : ComicItemGridViewModel {
        public override string[] SortSelectors => SortSelectorNames.ComicCollectionSortSelectorNames;
        protected ComicCollectionSortSelector SelectedSortSelector => (ComicCollectionSortSelector)this.SelectedSortIndex;

        private readonly ComicCollectionView properties;

        protected ComicNavigationItemGridViewModel(MainViewModel appViewModel, ComicCollectionView comicCollections)
            : base(appViewModel)
        {
            this.properties = comicCollections;
            this.properties.CollectionsChanged += this.Properties_CollectionsChanged;
        }
        
        public static ComicNavigationItemGridViewModel ForViewModel(MainViewModel mainViewModel, ComicCollectionView comicCollections) {
            var viewModel = new ComicNavigationItemGridViewModel(mainViewModel, comicCollections);
            // Sorts and loads the actual comic items
            viewModel.RefreshComicItems();
            return viewModel;
        }

        protected void RefreshComicItems() {
            var items = this.properties.Select(property =>
                new ComicNavigationItem(property.Name, property.Comics)
            );

            this.SetComicItems(items, this.properties.Count);
        }

        private protected override void SortOrderChanged() {
            this.properties.SetSort(this.SelectedSortSelector);
            this.RefreshComicItems();
        }

        public void NavigateIntoItem(ComicNavigationItem item) {
            this.MainViewModel.NavigateInto(item, parent: this);
        }

        private void Properties_CollectionsChanged(ComicCollectionView sender, CollectionsChangedEventArgs e) {
            switch (e.Type) {
                case CollectionsChangeType.ItemsChanged:
                    if (e.Added.Any()) {
                        var addedItems = e.Added.Select(name => new ComicNavigationItem(name, sender.GetView(name)));

                        foreach (var item in addedItems) {
                            this.ComicItems.Insert(0, item);
                        }
                    }

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

                    break;

                case CollectionsChangeType.Refresh:
                    this.RefreshComicItems();
                    break;

                default:
                    throw new ProgrammerError($"{nameof(ComicNavigationItemGridViewModel)}.{nameof(this.Properties_CollectionsChanged)}: unhandled switch case");
            }
        }

        public override void Dispose() {
            base.Dispose();

            this.properties.CollectionsChanged -= this.Properties_CollectionsChanged;
        }
    }
}
