using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicNavigationItemGridViewModel : ComicItemGridViewModel {
        public override string[] SortSelectors => SortSelectorNames.ComicPropertySortSelectorNames;
        private ComicPropertySortSelector SelectedSortSelector => (ComicPropertySortSelector)this.SelectedSortIndex;

        private readonly ComicView comics;
        // This is set during the constructor, but not by the constructor. We use this "hack" to mark the semantic difference.
        private OneTimeComicPropertiesView? comicProperties;
        public OneTimeComicPropertiesView ComicProperties => this.comicProperties!;

        public ComicNavigationItemGridViewModel(MainViewModel appViewModel, ComicView comics)
            : base(appViewModel)
        {
            this.comics = comics;

            // TODO implement events for ComicPropertiesView and replace this logic with more sophisticated logic.
            this.comics.ComicsChanged += this.Comics_ComicsChanged;

            // Sorts and loads the actual comic items
            this.RefreshComicItems();
        }

        private void RefreshComicItems() {
            this.SortOrderChanged();
        }

        private protected override void SortOrderChanged() {
            var view = this.GetOneTimeSortedProperties();
            this.comicProperties = view;

            var items = view.Select(property => 
                ComicItem.NavigationItem(property.Name, property.Comics, trackChangesFrom: view.PropertyView(property.Name))
            );

            this.SetComicItems(items);
        }

        public override Task OpenItemsAsync(IEnumerable<ComicItem> items) {
            if (items.Count() != 1) {
                throw new ProgrammerError("Should not allow the user to open multiple navigation " +
                                          "items at once (use the search into feature instead)");
            }

            this.MainViewModel.NavigateInto(items.First());

            return Task.CompletedTask;
        }

        private OneTimeComicPropertiesView GetOneTimeSortedProperties() {
            return comics.SortedProperties(
                this.NavigationTag switch {
                    NavigationTag.Author => comic => new[] { comic.DisplayAuthor },
                    NavigationTag.Category => comic => new[] { comic.DisplayCategory },
                    NavigationTag.Tags => comic => comic.Tags,
                    _ => throw new ProgrammerError("unhandled switch case")
                },
                this.SelectedSortSelector
            );
        }

        private void Comics_ComicsChanged(ComicView sender, ComicsChangedEventArgs e) {
            Debug.WriteLine($"VM{debug_this_count} {nameof(Comics_ComicsChanged)} called for view model {this.NavigationTag}");

            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                case ComicChangeType.Refresh:
                    this.RefreshComicItems();
                    break;

                case ComicChangeType.ThumbnailChanged:
                    break;

                default:
                    throw new ProgrammerError($"{nameof(ComicNavigationItemGridViewModel)}.{nameof(Comics_ComicsChanged)}: unhandled switch case");
            }
        }

        public override void Dispose() {
            base.Dispose();

            this.comics.ComicsChanged -= this.Comics_ComicsChanged;
        }
    }
}
