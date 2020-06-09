using ComicsLibrary;
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
    public class ComicItem {
        public string Title { get; }
        public string Subtitle { get; }
        public ComicItemType ItemType { get; }
        public IList<Comic> Comics { get; }

        public string ThumbnailPath => Thumbnail.ThumbnailPath(this.TitleComic);
        public Comic TitleComic => this.Comics[0];

        private ComicItem(string title, string subtitle, ComicItemType itemType, IList<Comic> comics) {
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

            var s = comics.Count() == 1 ? "" : "s";

            return new ComicItem(
                name,
                $"{comics.Count()} Item{s}",
                ComicItemType.Navigation,
                comics.ToList()
            );
        }
    }

    public enum ComicItemType {
        Work, Navigation
    }
}