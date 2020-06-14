using ComicsLibrary;
using ComicsViewer.Support.ClassExtensions;
using ComicsViewer.Thumbnails;
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
        public string Title { get; }
        public string Subtitle { get; private set; }
        public ComicItemType ItemType { get; }
        internal List<Comic> Comics { get; }

        public string ThumbnailPath => Thumbnail.ThumbnailPath(this.TitleComic);
        public Comic TitleComic => this.Comics[0];

        private ComicItem(string title, string subtitle, ComicItemType itemType, List<Comic> comics) {
            this.Title = title;
            this.Subtitle = subtitle;
            this.ItemType = itemType;
            this.Comics = comics;
        }

        public static ComicItem WorkItem(Comic comic) {
            return new ComicItem(
                comic.DisplayTitle,
                comic.DisplayAuthor,
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
                comics.Count().PluralString("Item"),
                ComicItemType.Navigation,
                comics.ToList()
            );;
        }

        /* ComicItem will not modify itself. If external code modifies ComicItem, it should call this method
         * to send a NotifyPropertyChanged event */
        public void DoNotifyPropertiesChanged() {
            if (this.ItemType == ComicItemType.Navigation) {
                this.Subtitle = this.Comics.Count().PluralString("Item");
            }

            this.OnPropertyChanged("");
        }
    }

    public enum ComicItemType {
        Work, Navigation
    }
}