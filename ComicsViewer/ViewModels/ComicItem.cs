using ComicsLibrary;
using ComicsLibrary.Collections;
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
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicItem : ViewModelBase {
        public string Title { get; set; }
        public ComicItemType ItemType { get; }
        internal List<Comic> Comics { get; }
        internal ComicView TrackingChangesFrom { get; }

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
        public BitmapImage ThumbnailImage { get; private set; }

        public Comic TitleComic {
            get {
                if (this.Comics.Count == 0) {
                    throw new ApplicationLogicException("ComicItem.TitleComic: ComicItem does not contain comics");
                }
                return this.Comics[0];
            }
        }

        private ComicItem(string title, ComicItemType itemType, List<Comic> comics, ComicView trackChangesFrom) {
            this.Title = title;
            this.ItemType = itemType;
            this.Comics = comics;
            this.ThumbnailImage = new BitmapImage { UriSource = new Uri(Thumbnail.ThumbnailPath(this.TitleComic)) };
            this.TrackingChangesFrom = trackChangesFrom;
            this.TrackingChangesFrom.ComicsChanged += this.View_ComicsChanged;
        }

        public static ComicItem WorkItem(Comic comic, ComicView trackChangesFrom) {
            return new ComicItem(
                comic.DisplayTitle,
                ComicItemType.Work,
                new List<Comic> { comic },
                trackChangesFrom
            );
        }

        public static ComicItem NavigationItem(string name, IEnumerable<Comic> comics, ComicView trackChangesFrom) {
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


        private async void View_ComicsChanged(ComicView sender, ComicsChangedEventArgs e) {
            // The new system is not implemented for nav items yet.
            if (this.ItemType == ComicItemType.Navigation) {
                return;
            }

            switch (e.Type) {  // switch ChangeType
                case ComicChangeType.ItemsChanged:
                    // Added: handled by ComicItemGrid

                    /* We don't need to worry about adding nav items since adding items means creating new work items or updating
                     * nav items, but adding comics trigger a nav page reload. This may change the in the future */
                    if (e.Modified.Count() > 0) {
                        /* We don't need to worry navigation items since adding items means creating new work items or updating
                         * nav items, but adding comics trigger a nav page reload. This may change the in the future */
                        var match = e.Modified.Where(comic => comic.UniqueIdentifier == this.TitleComic.UniqueIdentifier)
                                              .FirstOrNull();

                        if (match is Comic comic) {
                            this.Comics.Clear();
                            this.Comics.Add(comic);

                            this.Title = this.TitleComic.DisplayTitle;
                            this.OnPropertyChanged("");

                            // an item can't be modified and removed at the same time
                            return;
                        }
                    }

                    if (e.Removed.Count() > 0) {
                        // note: nav items don't implement the new system yet, but this scode is generalized.
                        var removalList = new List<Comic>();

                        foreach (var comic in this.Comics) {
                            if (e.Removed.Contains(comic)) {
                                removalList.Add(comic);
                            }
                        }

                        foreach (var comic in removalList) {
                            _ = this.Comics.Remove(comic);
                        }

                        if (this.Comics.Count == 0) {
                            // Remove this ComicItem
                            sender.ComicsChanged -= this.View_ComicsChanged;
                            this.RequestingRefresh(this, RequestingRefreshType.Remove);
                        } else {
                            this.OnPropertyChanged("");
                        }
                    }

                    return;

                case ComicChangeType.Refresh:
                    // the parent will have called refresh, so we don't need to do anything.
                    return;

                case ComicChangeType.ThumbnailChanged:
                    if (e.Modified!.Contains(this.TitleComic)) {
                        var image = new BitmapImage();
                        var thumbnailFile = await StorageFile.GetFileFromPathAsync(Thumbnail.ThumbnailPath(this.TitleComic));

                        using (var stream = await thumbnailFile.OpenReadAsync()) {
                            await image.SetSourceAsync(stream);
                        }

                        this.ThumbnailImage = image;
                        this.OnPropertyChanged(nameof(this.ThumbnailImage));
                    }

                    return;

                default:
                    throw new ApplicationLogicException($"{nameof(View_ComicsChanged)}: unhandled switch case");
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