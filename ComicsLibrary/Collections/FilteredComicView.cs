using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace ComicsLibrary.Collections {
    /// <summary>
    /// A view that is a filtered version of its parent.
    /// </summary>
    public class FilteredComicView : ComicView {
        private readonly ComicView filteredFrom;
        private readonly Func<Comic, bool> filter;

        internal FilteredComicView(ComicView filteredFrom, Func<Comic, bool> filter) : base(filteredFrom) {
            this.filteredFrom = filteredFrom;
            this.filter = filter;
        }

        public override bool Contains(Comic comic) => this.filter(comic) && this.filteredFrom.Contains(comic);
        public override int Count() => this.filteredFrom.Where(this.filter).Count();
        public override IEnumerator<Comic> GetEnumerator() => this.filteredFrom.Where(this.filter).GetEnumerator();

        public override Comic GetStored(Comic comic) {
            if (!this.filter(comic)) {
                throw new ArgumentException("comic doesn't exist in this collection");
            }

            return this.filteredFrom.GetStored(comic);
        }

        private protected override void ParentComicView_ViewChanged(ComicView sender, ViewChangedEventArgs e) {
            switch (e.Type) {  // switch ChangeType
                case ComicChangeType.ItemsChanged:
                    var add = e.Add.Where(this.filter);
                    var remove = e.Remove.Where(this.filter);

                    if (add.Count() > 0 || remove.Count() > 0) {
                        this.OnComicChanged(new ViewChangedEventArgs(e.Type, add, remove));
                    }

                    return;

                case ComicChangeType.Refresh:
                    this.OnComicChanged(e);
                    return;

                case ComicChangeType.ThumbnailChanged:
                    var changed = e.Add.Where(this.filter);
                    if (changed.Count() > 0) {
                        this.OnComicChanged(new ViewChangedEventArgs(e.Type, add: changed));
                    }
                    return;
 
                default:
                    throw new ProgrammerError($"{nameof(FilteredComicView)}.{nameof(ParentComicView_ViewChanged)}: unhandled switch case");
            }
        }
    }
}
