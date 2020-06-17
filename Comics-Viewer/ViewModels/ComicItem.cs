using ComicsLibrary;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Features;
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

        public Comic TitleComic => this.Comics[0];

        private ComicItem(string title, ComicItemType itemType, List<Comic> comics) {
            this.Title = title;
            this.ItemType = itemType;
            this.Comics = comics;
            this.ThumbnailPath = Thumbnail.ThumbnailPath(this.TitleComic);
        }

        public static ComicItem WorkItem(Comic comic) {
            return new ComicItem(
                comic.DisplayTitle,
                ComicItemType.Work,
                new List<Comic> { comic }
            );
        }

        public static ComicItem NavigationItem(string name, IEnumerable<Comic> comics) {
            if (comics.Count() == 0) {
                throw new ApplicationLogicException("ComicNavigationItem should not receive an empty IEnumerable in its constructor.");
            }

            return new ComicItem(
                name,
                ComicItemType.Navigation,
                comics.ToList()
            );;
        }

        /* ComicItem will not modify its own comics. If external code modifies ComicItem, it should call this method
         * to send a NotifyPropertyChanged event. Note that sorting and filtering may become temporarily broken if you
         * changed a property that's being sorted by/filtered by */
        public void DoNotifyUnderlyingComicsChanged() {
            if (this.ItemType != ComicItemType.Navigation) {
                this.Title = this.TitleComic.DisplayTitle;
            }

            this.OnPropertyChanged("");
        }

        public void DoNotifyThumbnailChanged() {
            this.ThumbnailPath = "ms-appx:///Assets/LargeTile.scale-200.png";
            this.OnPropertyChanged(nameof(this.ThumbnailPath));

            this.ThumbnailPath = Thumbnail.ThumbnailPath(this.TitleComic);
            this.OnPropertyChanged(nameof(this.ThumbnailPath));
        }
    }

    public enum ComicItemType {
        Work, Navigation
    }
}