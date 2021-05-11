using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsViewer.Features;
using System.Collections.Generic;

#nullable enable

namespace ComicsViewer.Support {
    public class MainComicList : ComicList {
        public readonly Filter Filter = new();

        public MainComicList() {
            this.Filter.FilterChanged += this.Filter_FilterChanged;
        }

        // another method purely for the purpose of UI
        public void NotifyThumbnailChanged(IEnumerable<Comic> comics) {
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ThumbnailChanged, comics));
        }

        private ComicView? _filtered;
        public ComicView Filtered() {
            this._filtered ??= this.Filtered(this.Filter.ShouldBeVisible);
            return this._filtered;
        }

        protected override void RefreshComics(IEnumerable<Comic> comics) {
            base.RefreshComics(comics);
            this._filtered = null;
        }

        private void Filter_FilterChanged(Filter filter) {
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.Refresh));
        }
    }
}
