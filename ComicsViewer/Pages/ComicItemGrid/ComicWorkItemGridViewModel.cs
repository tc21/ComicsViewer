using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.Uwp.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicWorkItemGridViewModel : ComicItemGridViewModel {
        public override string[] SortSelectors => SortSelectorNames.ComicSortSelectorNames;
        private ComicSortSelector SelectedSortSelector => (ComicSortSelector)this.SelectedSortIndex;

        public ComicItemGridViewModelProperties Properties { get; }

        private readonly SortedComicView comics;

        public ComicWorkItemGridViewModel(
            IMainPageContent parent,
            MainViewModel appViewModel,
            ComicView comics,
            ComicItemGridViewModelProperties? properties = null,
            ComicItemGridState? savedState = null
        ) : base(parent, appViewModel) {
            this.Properties = properties ?? new ComicItemGridViewModelProperties();

            this.comics = comics.Sorted(this.SelectedSortSelector);
            this.comics.ComicsChanged += this.Comics_ComicsChanged;

            // Track changes if this is a playlist
            if (this.Properties.PlaylistName is { } name) {
                var playlist = (Playlist)this.MainViewModel.Playlists.GetCollection(name);
                playlist.ComicsChanged += this.Comics_ComicsChanged;
            }

            // Sorts and loads the actual comic items
            if (savedState is not null) {
                this.SetComicItems(savedState.Items);
                this.RequestedInitialScrollOffset = savedState.ScrollOffset;
            } else {
                this.RefreshComicItems();
            }
        }

        /* We have an unfortunate discrepancy here between work and nav items, caused by how we implemented sorting:
         * You are supposed to call SortedComicView.Sort, which will then trigger events that call SetComicItems. So a
         * list of workItems is already sorted here. On the other hand, we have to manually sort our ComicPropertiesView,
         * because we didn't need to waste time working out an event-based ComicPropertiesView */
        public override void SortAndRefreshComicItems() {
            this.comics.Sort(this.SelectedSortSelector);
            this.RefreshComicItems();
        }

        private void RefreshComicItems() {
            this.SetComicItems(this.MakeComicItems(this.comics).ToList());
        }

        private IEnumerable<ComicWorkItem> MakeComicItems(IEnumerable<Comic> comics) {
            // we make a copy of comics, since the returned enumerable is expectedly to be lazily evaluated, and comics might change
            comics = comics.ToList();

            foreach (var comic in comics) {
                var item = new ComicWorkItem(comic, trackChangesFrom: this.comics);
                item.RequestingRefresh += this.ComicWorkItem_RequestingRefresh;
                yield return item;
            }
        }

        public async Task OpenItemsAsync(IEnumerable<ComicItem> items) {
            var workItems = items.Cast<ComicWorkItem>().ToList();

            if (workItems.Any(item => !this.ComicItems.Contains(item))) {
                throw new ProgrammerError("received items that are not part of this.ComicItems");
            }

            foreach (var item in workItems) {
                if (!(await this.MainViewModel.Profile.GetComicSubitemsAsync(item.Comic) is { } subitems)) {
                    return;
                }

                await Startup.OpenComicSubitemAsync(subitems.First(), this.MainViewModel.Profile);
            }
        }

        private void Comics_ComicsChanged(ComicView sender, ComicsChangedEventArgs e) {
            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                    if (e.Added.Any()) {
                        var addedItems = this.MakeComicItems(e.Added).ToList();

                        foreach (var item in addedItems) {
                            // TODO implement live sorting
                            this.ComicItems.Insert(0, item);
                        }

                        /* Generate thumbnails for added items */
                        /* There may be many view models active at any given moment. The if statement ensures that only
                         * the top level grid (guaranteed to be unique) requests thumbnails to be generated */
                        if (this.NavigationPageType is NavigationPageType.Root) {
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
                    throw new ProgrammerError($"{nameof(ComicWorkItemGridViewModel)}.{nameof(this.Comics_ComicsChanged)}: unhandled switch case");
            }
        }


        /* The ComicItem.RequestingRefresh event is used for items to request themselves be reloaded or removed. 
         * This will most likely be moved to ...WorkItemViewModel when the new ComicPropertiesView events are implemented */
        private void ComicWorkItem_RequestingRefresh(ComicWorkItem sender, ComicWorkItem.RequestingRefreshType type) {
            if (!this.ComicItems.Contains(sender)) {
                return;
            }

            switch (type) {
                case ComicWorkItem.RequestingRefreshType.Reload:
                    var index = this.ComicItems.IndexOf(sender);
                    this.ComicItems.RemoveAt(index);
                    this.ComicItems.Insert(index, sender);
                    break;
                case ComicWorkItem.RequestingRefreshType.Remove:
                    sender.RequestingRefresh -= this.ComicWorkItem_RequestingRefresh;
                    _ = this.ComicItems.Remove(sender);

                    if (this.ComicItems.Count == 0 && this.NavigationPageType is not NavigationPageType.Root) {
                        this.MainViewModel.TryNavigateOut();
                    }

                    break;
                default:
                    throw new ProgrammerError("Unhandled switch case");
            }
        }

        #region Commands - work items

        public void StartRequestGenerateThumbnailsTask(IEnumerable<ComicWorkItem> comicItems, bool replace = false) {
            var copy = comicItems.ToList();
            _ = this.MainViewModel.StartUniqueTaskAsync(
                "thumbnail", $"Generating thumbnails for {copy.Count} items...",
                (cc, p) => this.GenerateAndApplyThumbnailsInBackgroundThreadAsync(copy, replace, cc, p),
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public async Task ToggleLovedStatusForComicsAsync(IEnumerable<ComicWorkItem> selectedItems) {
            var comics = selectedItems.Select(item => item.Comic).ToList();
            var newStatus = !comics.All(item => item.Loved);
            var changes = comics.Select(comic => comic.WithMetadata(loved: newStatus));

            await this.MainViewModel.UpdateComicAsync(changes);
        }

        #endregion

        public override void Dispose() {
            base.Dispose();

            this.comics.ComicsChanged -= this.Comics_ComicsChanged;
        }
    }
}
