using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
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
            this.RefreshComicItems();
            
        }

        public void NavigateIntoItem(ComicNavigationItem item) {
            this.MainViewModel.NavigateInto(item, parent: this);
        }

        private void Properties_CollectionsChanged(ComicCollectionView sender, CollectionsChangedEventArgs e) {
            switch (e.Type) {
                case CollectionsChangeType.ItemsChanged:
                    // TODO
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
