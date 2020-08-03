using ComicsLibrary;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Features;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicItem : ViewModelBase {
        public string Title { get; set; }
        public ComicItemType ItemType { get; }
        internal List<Comic> Comics { get; }

        public string Subtitle => this.ItemType switch {
            ComicItemType.Work => this.TitleComic.DisplayAuthor,
            ComicItemType.Navigation => this.Comics.Count().PluralString("Item"),
            _ => throw new ApplicationLogicException()
        };

        public bool IsLoved => this.ItemType switch {
            ComicItemType.Work => this.TitleComic.Loved,
            _ => false
        };

        public bool IsDisliked => this.ItemType switch {
            ComicItemType.Work => this.TitleComic.Disliked,
            _ => false
        };

        /* manually managed so it can be refreshed */
        public string ThumbnailPath { get; private set; }

        public Comic TitleComic {
            get {
                if (this.Comics.Count == 0) {
                    throw new ApplicationLogicException("ComicItem.TitleComic: ComicItem does not contain comics");
                }
                return this.Comics[0];
            }
        }

        private ComicItem(string title, ComicItemType itemType, List<Comic> comics, MainViewModel? trackChangesFrom) {
            this.Title = title;
            this.ItemType = itemType;
            this.Comics = comics;
            this.ThumbnailPath = Thumbnail.ThumbnailPath(this.TitleComic);

            if (trackChangesFrom is MainViewModel viewModel) {
                viewModel.ComicsModified += this.MainViewModel_ComicsModified;
            }
        }

        public static ComicItem WorkItem(Comic comic, MainViewModel? trackChangesFrom) {
            return new ComicItem(
                comic.DisplayTitle,
                ComicItemType.Work,
                new List<Comic> { comic },
                trackChangesFrom
            );
        }

        public static ComicItem NavigationItem(string name, IEnumerable<Comic> comics, MainViewModel? trackChangesFrom) {
            if (comics.Count() == 0) {
                throw new ApplicationLogicException("ComicNavigationItem should not receive an empty IEnumerable in its constructor.");
            }

            return new ComicItem(
                name,
                ComicItemType.Navigation,
                comics.ToList(),
                trackChangesFrom
            ); ;
        }

        private void MainViewModel_ComicsModified(MainViewModel sender, ComicsModifiedEventArgs e) {
            switch (e.ModificationType) {
                case ComicModificationType.ItemsAdded:
                    /* We don't need to worry about this since adding items means creating new work items or updating
                     * nav items, but adding comics trigger a nav page reload. This may change the in the future */
                    return;

                case ComicModificationType.ItemsChanged:
                    if (this.ItemType == ComicItemType.Navigation) {
                        /* Since (1) we cannot modify display author and category, we don't need to worry about updating 
                         * navigation comic items. This will not always be the case. */
                        return;
                    }

                    if (e.ModifiedComics.Contains(this.TitleComic)) {
                        var comic = e.ModifiedComics.GetStoredComic(this.TitleComic);

                        if (comic != this.TitleComic) {
                            this.Comics.Clear();
                            this.Comics.Add(comic);
                        }

                        this.Title = this.TitleComic.DisplayTitle;
                        this.OnPropertyChanged("");

                        if (e.ShouldReloadComics) {
                            this.RequestingRefresh(this, RequestingRefreshType.Reload);
                        }
                    }

                    return;

                case ComicModificationType.ItemsRemoved:
                    var removalList = new List<Comic>();

                    foreach (var comic in this.Comics) {
                        if (e.ModifiedComics.Contains(comic)) {
                            removalList.Add(comic);
                        }
                    }

                    foreach (var comic in removalList) {
                        this.Comics.Remove(comic);
                    }

                    if (this.Comics.Count == 0) {
                        // Remove this ComicItem
                        sender.ComicsModified -= this.MainViewModel_ComicsModified;
                        this.RequestingRefresh(this, RequestingRefreshType.Remove);
                    } else {
                        this.OnPropertyChanged("");
                    }

                    return;

                default:
                    throw new ApplicationLogicException("Unhandled switch case");
            }
        }

        public delegate void RequestingRefreshEventArgs(ComicItem sender, RequestingRefreshType type);
        public event RequestingRefreshEventArgs RequestingRefresh = delegate { };
        public enum RequestingRefreshType {
            Reload, Remove
        }
    }

    public enum ComicItemType {
        Work, Navigation
    }
}