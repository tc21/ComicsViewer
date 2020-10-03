using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicNavigationItem : ComicItem {
        public ComicView Comics { get; }

        public override string Title { get; }
        public override string Subtitle => this.Comics.Count().PluralString("Item");
        public override bool IsLoved => false;

        public override IEnumerable<Comic> ContainedComics() => this.Comics;

        public ComicNavigationItem(string name, ComicView comics) {
            if (comics.Count() == 0) {
                throw new ProgrammerError("ComicNavigationItem should not receive an empty ComicView in its constructor.");
            }

            this.Title = name;
            this.Comics = comics;

            this.ThumbnailImage = new BitmapImage { UriSource = new Uri(Thumbnail.ThumbnailPath(comics.First())) };
        }

        /* TODO:
         * ComicNavigationItem does not handle any events. It doesn't remove itself from its parent. It's not smart.
         * I haven't implemented it yet. Currently, modifying nav items just triggers a page reload. In the future,
         * we will implement nav item events (see ComicPropertiesView). */

        public override void Dispose() {
            // do nothing
        }
    }
}
