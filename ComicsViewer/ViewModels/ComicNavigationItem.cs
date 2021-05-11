using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicNavigationItem : ComicItem {
        public ComicView Comics { get; }

        public override string Title { get; }
        public override string Subtitle => this.Comics.Count().PluralString("Item");
        public override bool IsLoved => false;

        public override IEnumerable<Comic> ContainedComics() => this.Comics;

        private Comic? thumbnailComic;

        public ComicNavigationItem(string name, ComicView comics) {
            this.Title = name;
            this.Comics = comics;

            if (comics.Any()) {
                var thumbnailComic = comics.First();
                this.ThumbnailImageSource = new Uri(Thumbnail.ThumbnailPath(thumbnailComic));
            }

            comics.ComicsChanged += this.Comics_ComicsChanged;
        }

        private async void Comics_ComicsChanged(ComicView sender, ComicsChangedEventArgs e) {
            switch (e.Type) {
                case ComicChangeType.ItemsChanged:
                    if (this.Comics.Any() && this.Comics.First() != this.thumbnailComic) {
                        this.thumbnailComic = this.Comics.First();
                        this.ThumbnailImageSource = new Uri(Thumbnail.ThumbnailPath(this.thumbnailComic));
                        this.OnPropertyChanged(nameof(this.ThumbnailImageSource));
                    }

                    this.OnPropertyChanged(nameof(this.Subtitle));

                    // note: title cannot be changed. Changing title means removing this item and adding a new one.
                    break;

                case ComicChangeType.ThumbnailChanged:
                    if (this.thumbnailComic is not null && e.Modified.Contains(this.thumbnailComic)) {
                        await this.RefreshImageSourceAsync();
                    }

                    break;

                case ComicChangeType.Refresh:
                    // the parent's job, not ours
                    break;

                default:
                    throw new ProgrammerError($"{nameof(this.Comics_ComicsChanged)}: unhandled switch case");
            }
        }

        /* TODO:
         * ComicNavigationItem does not handle any events. It doesn't remove itself from its parent. It's not smart.
         * I haven't implemented it yet. Currently, modifying nav items just triggers a page reload. In the future,
         * we will implement nav item events (see ComicPropertiesView). */

        ~ComicNavigationItem() {
            this.Comics.ComicsChanged -= this.Comics_ComicsChanged;
        }
    }
}
