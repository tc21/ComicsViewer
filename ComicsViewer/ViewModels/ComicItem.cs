using ComicsLibrary;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Features;
using ComicsViewer.Support;
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

        private ComicItem(string title, ComicItemType itemType, List<Comic> comics, ComicView? trackChangesFrom) {
            this.Title = title;
            this.ItemType = itemType;
            this.Comics = comics;
            this.ThumbnailPath = Thumbnail.ThumbnailPath(this.TitleComic);

            if (trackChangesFrom is ComicView view) {
                view.ComicChanged += this.View_ComicChanged;
            }
        }

        public static ComicItem WorkItem(Comic comic, ComicView? trackChangesFrom) {
            return new ComicItem(
                comic.DisplayTitle,
                ComicItemType.Work,
                new List<Comic> { comic },
                trackChangesFrom
            );
        }

        public static ComicItem NavigationItem(string name, IEnumerable<Comic> comics, ComicView? trackChangesFrom) {
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

        private void View_ComicChanged(ComicView sender, ComicChangedEventArgs args) {
            switch (args.Type) {
                case ComicChangedType.Add:
                    /* We don't need to worry about this since adding items means creating new work items or updating
                     * nav items, but adding comics trigger a nav page reload. This may change the in the future */
                    return;

                case ComicChangedType.Modified:
                    if (this.ItemType == ComicItemType.Navigation) {
                        /* Since (1) we cannot modify display author and category, we don't need to worry about updating 
                         * navigation comic items. This will not always be the case. */
                        return;
                    }

                    // must be work item
                    var match = args.Comics!.Where(comic => comic.UniqueIdentifier == this.TitleComic.UniqueIdentifier)
                                            .FirstOrDefault();

                    if (match == null) {
                        return;
                    }

                    if (match != this.TitleComic) {
                        this.Comics.Clear();
                        this.Comics.Add(match);
                    }

                    this.Title = this.TitleComic.DisplayTitle;
                    this.OnPropertyChanged("");

                    return;

                case ComicChangedType.Remove:
                    var removalList = new List<Comic>();

                    foreach (var comic in this.Comics) {
                        if (args.Comics!.Contains(comic)) {
                            removalList.Add(comic);
                        }
                    }

                    foreach (var comic in removalList) {
                        _ = this.Comics.Remove(comic);
                    }

                    if (this.Comics.Count == 0) {
                        // Remove this ComicItem
                        sender.ComicChanged -= this.View_ComicChanged;
                        this.RequestingRefresh(this, RequestingRefreshType.Remove);
                    } else {
                        this.OnPropertyChanged("");
                    }

                    return;

                case ComicChangedType.Refresh:
                    // the parent will have call refresh, so we don't need to do anything.
                    return;

                default:
                    throw new ApplicationLogicException($"{nameof(View_ComicChanged)}: unhandled switch case");
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