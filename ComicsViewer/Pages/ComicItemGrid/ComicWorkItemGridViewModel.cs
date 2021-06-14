using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Support;
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
            MainViewModel mainViewModel,
            ComicView comics,
            ComicItemGridViewModelProperties? properties = null,
            ComicItemGridState? savedState = null
        ) : base(parent, mainViewModel) {
            this.Properties = properties ?? new ComicItemGridViewModelProperties();

            this.comics = comics.Sorted(this.SelectedSortSelector);
            this.comics.ComicsChanged += this.Comics_ComicsChanged;

            // Track changes if this is a playlist
            if (this.Properties.PlaylistName is { } name) {
                var playlist = (Playlist)this.MainViewModel.Playlists.GetCollection(name);
                playlist.ComicsChanged += this.Comics_ComicsChanged;
            }

            if (savedState?.LastModified is { } lastModified && lastModified == mainViewModel.LastModified) {
                this.SetComicItems(savedState.Items);
            } else {
                this.RefreshComicItems();
            }

            if (savedState?.ScrollOffset is { } offset) {
                this.RequestedInitialScrollOffset = offset;
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

        private protected override void SetComicItems(IEnumerable<ComicItem> items) {
            var actualItems = items.Cast<ComicWorkItem>().ToList();
            base.SetComicItems(actualItems);
        }

        private void RefreshComicItems() {
            this.SetComicItems(this.MakeComicItems(this.comics).ToList());
        }

        private void AddComicItem(ComicWorkItem item, int? index = null) {
            if (index is { } i) {
                this.ComicItems.Insert(i, item);
            } else {
                this.ComicItems.Add(item);
            }
        }

        private void RemoveComicItem(ComicWorkItem item) {
            if (!this.ComicItems.Remove(item)) {
                throw new ProgrammerError("Removing a ComicWorkItem that didn't already exist.");
            }
        }

        private void RemoveComicItem(int index) {
            this.ComicItems.RemoveAt(index);
        }

        private IEnumerable<ComicWorkItem> MakeComicItems(IEnumerable<Comic> comics) {
            // we make a copy of comics, since the returned enumerable is expectedly to be lazily evaluated, and comics might change
            comics = comics.ToList();
            return comics.Select(comic => this.MainViewModel.Comics.GetWorkItem(comic));
        }

        public async Task OpenItemsAsync(IEnumerable<ComicItem> items) {
            var workItems = items.Cast<ComicWorkItem>().ToList();

            if (workItems.Any(item => !this.ComicItems.Contains(item))) {
                throw new ProgrammerError("received items that are not part of this.ComicItems");
            }

            foreach (var item in workItems) {
                if (await this.MainViewModel.Profile.GetComicSubitemsAsync(item.Comic) is not { } subitems) {
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
                            this.AddComicItem(item, 0);
                        }

                        /* Generate thumbnails for added items */
                        /* There may be many view models active at any given moment. The if statement ensures that only
                         * the top level grid (guaranteed to be unique) requests thumbnails to be generated */
                        if (this.NavigationPageType is NavigationPageType.Root) {
                            this.ScheduleGenerateThumbnails(e.Added);
                        }
                    }

                    if (e.Removed.Any()) {
                        foreach (var comic in e.Removed) {
                            var (_, index) = this.ComicItems.Cast<ComicWorkItem>()
                                .Select((item, index) => (item, index))
                                .First(e => e.item.Comic.UniqueIdentifier == comic.UniqueIdentifier);

                            this.RemoveComicItem(index);
                        }

                        if (this.ComicItems.Count == 0 && this.NavigationPageType is not NavigationPageType.Root) {
                            this.MainViewModel.TryNavigateOut();
                        }
                    }

                    /* Note: if an item was modified, the change is already propagated through bindings */
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

        #region Commands - work items

        public async Task ToggleLovedStatusForComicsAsync(IEnumerable<ComicWorkItem> selectedItems) {
            var comics = selectedItems.Select(item => item.Comic).ToList();
            var newStatus = !comics.All(item => item.Loved);
            var changes = comics.Select(comic => comic.WithMetadata(loved: newStatus));

            await this.MainViewModel.UpdateComicAsync(changes);
        }

        #endregion

        public override void RemoveEventHandlers() {
            base.RemoveEventHandlers();

            this.comics.ComicsChanged -= this.Comics_ComicsChanged;

            if (this.Properties.PlaylistName is { } name) {
                var playlist = (Playlist)this.MainViewModel.Playlists.GetCollection(name);
                playlist.ComicsChanged -= this.Comics_ComicsChanged;
            }
        }
    }
}
