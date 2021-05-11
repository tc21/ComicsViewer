using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ComicsViewer.Common;

#nullable enable

namespace ComicsLibrary.Collections {
    /// <summary>
    /// A view that is a filtered version of its parent.
    /// </summary>
    public class FilteredComicView : ComicView {
        private readonly ComicView filteredFrom;
        private readonly Func<Comic, bool> filter;

        private readonly ComicList cache = new();

        internal FilteredComicView(ComicView filteredFrom, Func<Comic, bool> filter) : base(filteredFrom) {
            this.filteredFrom = filteredFrom;
            this.filter = filter;

            this.UpdateCache();
        }

        protected void UpdateCache() {
            this.cache.Refresh(this.filteredFrom.Where(this.filter));
        }

        public override bool Contains(string uniqueIdentifier) => this.cache.Contains(uniqueIdentifier);
        public override int Count => this.cache.Count;
        public override IEnumerator<Comic> GetEnumerator() => this.cache.GetEnumerator();

        public override Comic GetStored(string uniqueIdentifier) {
            if (!this.Contains(uniqueIdentifier)) {
                throw new ArgumentException("comic doesn't exist in this collection");
            }

            return this.filteredFrom.GetStored(uniqueIdentifier);
        }

        private protected override void ParentComicView_ViewChanged(ComicView sender, ViewChangedEventArgs e) {
            switch (e.Type) {  // switch ChangeType
                case ComicChangeType.ItemsChanged:
                    this.UpdateCache();

                    var add = e.Add.Where(this.filter).ToList();
                    var remove = e.Remove.Where(this.filter).ToList(); 

                    if (add.Any() || remove.Any()) {
                        this.OnComicChanged(new ViewChangedEventArgs(e.Type, add, remove));
                    }

                    return;

                case ComicChangeType.Refresh:
                    this.UpdateCache();

                    this.OnComicChanged(e);
                    return;

                case ComicChangeType.ThumbnailChanged:
                    var changed = e.Add.Where(this.filter).ToList();
                    if (changed.Any()) {
                        this.OnComicChanged(new ViewChangedEventArgs(e.Type, add: changed));
                    }
                    return;
 
                default:
                    throw new ProgrammerError($"{nameof(FilteredComicView)}.{nameof(this.ParentComicView_ViewChanged)}: unhandled switch case");
            }
        }
    }
}
