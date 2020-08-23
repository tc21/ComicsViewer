using ComicsLibrary;
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

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicWorkItemGridViewModel : ComicItemGridViewModel {
        public override string[] SortSelectors => SortSelectorNames.ComicSortSelectorNames;
        private ComicSortSelector SelectedSortSelector => (ComicSortSelector)this.SelectedSortIndex;

        private readonly SortedComicView comics;
        public ComicView Comics => this.comics;

        public ComicWorkItemGridViewModel(MainViewModel appViewModel, ComicView comics) 
            : base(appViewModel) 
        {
            this.comics = comics.Sorted(this.SelectedSortSelector);
            this.comics.ComicsChanged += this.Comics_ComicsChanged;

            // Sorts and loads the actual comic items
            this.RefreshComicItems();
        }


        /* We have an unfortunate discrepancy here between work and nav items, caused by how we implemented sorting:
         * You are supposed to call SortedComicView.Sort, which will then trigger events that call SetComicItems. So a
         * list of workItems is already sorted here. On the other hand, we have to manually sort our ComicPropertiesView,
         * because we didn't need to waste time working out an event-based ComicPropertiesView */
        private protected override void SortOrderChanged() {
            this.comics.Sort(this.SelectedSortSelector);
            this.RefreshComicItems();
        }

        private void RefreshComicItems() {
            this.SetComicItems(this.comics.Select(comic => 
                ComicItem.WorkItem(comic, trackChangesFrom: this.comics)));
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

        private void Comics_ComicsChanged(ComicView sender, ComicsChangedEventArgs e) {
            Debug.WriteLine($"VM{debug_this_count} {nameof(Comics_ComicsChanged)} called for view model {this.NavigationTag}");

            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                    if (e.Added.Count() > 0) {
                        var addedItems = e.Added.Select(comic =>
                            ComicItem.WorkItem(comic, trackChangesFrom: this.comics));

                        foreach (var item in addedItems) {
                            this.ComicItems.Insert(0, item);
                        }

                        /* Generate thumbnails for added items */
                        /* There may be many view models active at any given moment. The if statement ensures that only
                         * the top level grid (guaranteed to be unique) requests thumbnails to be generated */
                        if (this.NavigationTag != NavigationTag.Detail) {
                            this.StartRequestGenerateThumbnailsTask(addedItems);
                        }
                    }

                    /* individual ComicItems will call ComicItem_RequestingRefresh to update or remove themselves, 
                     * so we don't need any logic in this section to handle modified and removed. */
                    break;

                case ComicChangeType.Refresh:
                    this.RefreshComicItems();
                    break;

                case ComicChangeType.ThumbnailChanged:
                    break;

                default:
                    throw new ProgrammerError($"{nameof(ComicWorkItemGridViewModel)}.{nameof(Comics_ComicsChanged)}: unhandled switch case");
            }
        }

        #region Commands - work items

        public void StartRequestGenerateThumbnailsTask(IEnumerable<ComicItem> comicItems, bool replace = false) {
            var copy = comicItems.ToList();
            _ = this.MainViewModel.StartUniqueTaskAsync(
                "thumbnail", $"Generating thumbnails for {copy.Count} items...",
                (cc, p) => this.GenerateAndApplyThumbnailsInBackgroundThreadAsync(copy, replace, cc, p),
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public async Task ToggleDislikedStatusForComicsAsync(IEnumerable<ComicItem> selectedItems) {
            var comics = selectedItems.Select(item => item.TitleComic).ToList();
            var newStatus = !comics.All(item => item.Disliked);

            var changes = selectedItems.Select(item => item.TitleComic.WithUpdatedMetadata(metadata => {
                metadata.Disliked = newStatus;
                return metadata;
            }));

            await this.MainViewModel.UpdateComicAsync(changes);
        }

        public async Task ToggleLovedStatusForComicsAsync(IEnumerable<ComicItem> selectedItems) {
            var comics = selectedItems.Select(item => item.TitleComic).ToList();
            var newStatus = !comics.All(item => item.Loved);

            var changes = selectedItems.Select(item => item.TitleComic.WithUpdatedMetadata(metadata => {
                metadata.Loved = newStatus;
                return metadata;
            }));

            await this.MainViewModel.UpdateComicAsync(changes);
        }

        #endregion

        public override void Dispose() {
            base.Dispose();

            this.comics.ComicsChanged -= this.Comics_ComicsChanged;
        }
    }
}
