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
            var comicItems = this.comics.Select(comic => this.MainViewModel.Comics.GetWorkItem(comic));
            this.SetComicItems(comicItems);
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
                    foreach (var comic in e.Removed) {
                        if (IndexOf(comic) is { } index) {
                            this.ComicItems.RemoveAt(index);
                        }
                    }

                    // we are receiving this event because this.comics just changed, and e.Added are the items that
                    // aren't in this.ComicItems already. Since everything else is sorted, we can take a shortcut by
                    // removing unsorted items, sorting the additions, and then binary searching this.comics:
                    if (this.SelectedSortSelector is not ComicSortSelector.Random) {
                        var additions = new ComicList();

                        foreach (var comic in e.Modified) {
                            if (IndexOf(comic) is { } index) {
                                additions.Add(((ComicWorkItem)this.ComicItems[index]).Comic);
                                this.ComicItems.RemoveAt(index);
                            }
                        }

                        additions.Add(e.Added);

                        foreach (var comic in additions.Sorted(this.SelectedSortSelector)) {
                            var position = this.comics.IndexOf(comic) ?? 0;
                            this.ComicItems.Insert(position, this.MainViewModel.Comics.GetWorkItem(comic));
                        }
                    } else {
                        foreach (var comic in e.Added) {
                            this.ComicItems.Insert(0, this.MainViewModel.Comics.GetWorkItem(comic));
                        }
                    }

                    /* Generate thumbnails for added items */
                    /* There may be many view models active at any given moment. The if statement ensures that only
                     * the top level grid (guaranteed to be unique) requests thumbnails to be generated */
                    if (e.Added.Any() && this.NavigationPageType is NavigationPageType.Root) {
                        this.ScheduleGenerateThumbnails(e.Added);
                    }

                    if (this.ComicItems.Count == 0 && this.NavigationPageType is not NavigationPageType.Root) {
                        this.MainViewModel.TryNavigateOut();
                    }

                    break;

                case ComicChangeType.Refresh:
                    this.RefreshComicItems();
                    break;

                case ComicChangeType.ThumbnailChanged:
                    break;

                default:
                    throw new ProgrammerError($"{nameof(ComicWorkItemGridViewModel)}.{nameof(this.Comics_ComicsChanged)}: unhandled switch case");
            }

            int? IndexOf(Comic comic) {
                foreach (var (item, index) in this.ComicItems.Cast<ComicWorkItem>().Select((item, index) => (item, index))) {
                    if (item.Comic.UniqueIdentifier == comic.UniqueIdentifier) {
                        return index;
                    }
                }

                return null;
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
        }
    }
}
