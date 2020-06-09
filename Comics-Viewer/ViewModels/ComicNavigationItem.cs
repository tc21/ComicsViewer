using ComicsLibrary;
using ComicsViewer.Thumbnails;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicNavigationItem : ComicItem {
        public override string Title { get; }
        public override string Subtitle { get; }
        public override string ThumbnailPath => Thumbnail.ThumbnailPath(this.Comics.First());

        public override IEnumerable<Comic> Comics { get; }

        public ComicNavigationItem(string name, IEnumerable<Comic> comics) {
            this.Comics = comics.ToList();

            if (this.Comics.Count() == 0) {
                throw new ApplicationLogicException("ComicNavigationItem should not receive an empty IEnumerable in its constructor.");
            }

            this.Title = name;
            this.Subtitle = $"{this.Comics.Count()} Item";
            if (this.Comics.Count() != 1) {
                this.Subtitle += "s";
            }
        }
    }
}
