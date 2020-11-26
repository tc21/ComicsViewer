using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using ComicsViewer.Support;
using System.Linq;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicNavigationItemGridViewModel : ComicItemGridViewModel {
        public override string[] SortSelectors => SortSelectorNames.ComicPropertySortSelectorNames;
        protected ComicPropertySortSelector SelectedSortSelector => (ComicPropertySortSelector)this.SelectedSortIndex;

        private readonly ComicPropertiesView properties;

        protected ComicNavigationItemGridViewModel(MainViewModel appViewModel, ComicView comics)
            : base(appViewModel)
        {
            this.properties = this.GetSortedProperties(comics);

            this.properties.PropertiesChanged += this.Properties_PropertiesChanged;
        }

        public static ComicNavigationItemGridViewModel ForViewModel(MainViewModel mainViewModel, ComicView comics) {
            var viewModel = new ComicNavigationItemGridViewModel(mainViewModel, comics);
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

        private ComicPropertiesView GetSortedProperties(ComicView comics) {
            return comics.SortedProperties(
                this.NavigationTag switch {
                    NavigationTag.Author => comic => new[] { comic.Author },
                    NavigationTag.Category => comic => new[] { comic.Category },
                    NavigationTag.Tags => comic => comic.Tags,
                    _ => throw new ProgrammerError("unhandled switch case")
                },
                this.SelectedSortSelector
            );
        }

        protected void Properties_PropertiesChanged(ComicPropertiesView sender, PropertiesChangedEventArgs e) {
            switch (e.Type) {
                case PropertiesChangeType.ItemsChanged:
                    // TODO
                case PropertiesChangeType.Refresh:
                    this.RefreshComicItems();
                    break;

                default:
                    throw new ProgrammerError($"{nameof(ComicNavigationItemGridViewModel)}.{nameof(this.Properties_PropertiesChanged)}: unhandled switch case");
            }
        }

        public override void Dispose() {
            base.Dispose();

            this.properties.PropertiesChanged -= this.Properties_PropertiesChanged;
        }
    }
}
