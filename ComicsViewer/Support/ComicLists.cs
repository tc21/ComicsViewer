using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsViewer.Features;
using System.Collections.Generic;

#nullable enable

namespace ComicsViewer.Support {
    public class MainComicList : ComicList {
        public readonly Filter Filter = new Filter();

        public MainComicList() {
            this.Filter.FilterChanged += this.Filter_FilterChanged;
        }

        // another method purely for the purpose of UI
        public void NotifyThumbnailChanged(IEnumerable<Comic> comics) {
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ThumbnailChanged, comics));
        }

        private ComicView? filtered;
        public ComicView Filtered() {
            this.filtered ??= this.Filtered(this.Filter.ShouldBeVisible);
            return this.filtered;
        }

        private void Filter_FilterChanged(Filter filter) {
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.Refresh));
        }
    }
}
