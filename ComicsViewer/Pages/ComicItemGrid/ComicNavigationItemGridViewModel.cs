using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Features;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media;

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

        public override async Task OpenItemsAsync(IEnumerable<ComicItem> items) {
            // Although we don't have to await these, we will need to do so for it to throw an 
            // UnauthorizedAccessException when broadFileSystemAccess isn't enabled.
            try {
                var tasks = items.Select(item => Startup.OpenComicAtPathAsync(item.TitleComic.Path, this.MainViewModel.Profile));
                await Task.WhenAll(tasks);
            } catch (UnauthorizedAccessException) {
                await ExpectedExceptions.UnauthorizedFileSystemAccessAsync();
            } catch (FileNotFoundException e) {
                if (items.Count() == 1) {
                    await ExpectedExceptions.ComicNotFoundAsync(items.First().TitleComic);
                } else {
                    await ExpectedExceptions.FileNotFoundAsync(e.FileName, "The folder for an item could not be found.", cancelled: false);
                }
            }
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
