using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using ComicsViewer.Support;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicNavigationItemGridViewModel : ComicItemGridViewModel {
        public override string[] SortSelectors => SortSelectorNames.ComicPropertySortSelectorNames;
        private ComicPropertySortSelector SelectedSortSelector => (ComicPropertySortSelector)this.SelectedSortIndex;

        private readonly ComicView comics;
        private readonly List<Playlist> playlists;

        public ComicNavigationItemGridViewModel(MainViewModel appViewModel, ComicView comics, List<Playlist> playlists)
            : base(appViewModel)
        {
            this.comics = comics;
            this.playlists = playlists;

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

            var items = view.Select(property => 
                new ComicNavigationItem(property.Name, view.PropertyView(property.Name))
            );

            this.SetComicItems(items, view.Count);
        }

        public void NavigateIntoItem(ComicNavigationItem item) {
            this.MainViewModel.NavigateInto(item);
        }

        private OneTimeComicPropertiesView GetOneTimeSortedProperties() {
            return this.comics.SortedProperties(
                this.NavigationTag switch {
                    NavigationTag.Author => comic => new[] { comic.Author },
                    NavigationTag.Category => comic => new[] { comic.Category },
                    NavigationTag.Tags => comic => comic.Tags,
                    NavigationTag.Playlist => TempGetComicPlaylists,
                    _ => throw new ProgrammerError("unhandled switch case")
                },
                this.SelectedSortSelector
            );

            IEnumerable<string> TempGetComicPlaylists(Comic comic) {
                foreach (var playlist in this.playlists) {
                    if (playlist.Comics.Contains(comic)) {
                        yield return playlist.Name;
                    }
                }
            }
        }

        private void Comics_ComicsChanged(ComicView sender, ComicsChangedEventArgs e) {
            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                case ComicChangeType.Refresh:
                    this.RefreshComicItems();
                    break;

                case ComicChangeType.ThumbnailChanged:
                    break;

                default:
                    throw new ProgrammerError($"{nameof(ComicNavigationItemGridViewModel)}.{nameof(this.Comics_ComicsChanged)}: unhandled switch case");
            }
        }

        public override void Dispose() {
            base.Dispose();

            this.comics.ComicsChanged -= this.Comics_ComicsChanged;
        }
    }
}
