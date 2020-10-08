using System;
using System.Collections.Generic;
using System.Linq;

namespace ComicsLibrary.Collections {
    /// <summary>
    /// A list of comics, whose items can be freely added and removed at will.
    /// </summary>
    public class ComicList : MutableComicView {
        private readonly Dictionary<string, Comic> comics = new Dictionary<string, Comic>();

        public ComicList() : base(null) { }
        
        public ComicList(IEnumerable<Comic> comics) : base(null) {
            this.AddComics(comics);
        }

        private protected override void AddComic(Comic comic)
            => this.comics.Add(comic.UniqueIdentifier, comic);

        private protected override void RemoveComic(Comic comic) {
            if (!this.comics.Remove(comic.UniqueIdentifier)) {
                throw new ArgumentException("comic doesn't exist in this collection");
            }
        }

        private protected override void RefreshComics(IEnumerable<Comic> comics) {
            this.comics.Clear();

            foreach (var comic in comics) {
                this.comics.Add(comic.UniqueIdentifier, comic);
            }
        }

        public void Add(IEnumerable<Comic> comics) {
            var add = comics.ToList();
            this.AddComics(add);
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, add: add));
        }

        public void Remove(IEnumerable<Comic> comics) {
            var remove = comics.ToList();
            this.RemoveComics(remove);
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, remove: remove));
        }

        public void Add(Comic comic) => this.Add(new[] { comic });

        // commented out because it's not used: reenable when needed
        // public void Remove(Comic comic) => this.Remove(new[] { comic });

        // you should pass in the new list of comics
        public void Modify(IEnumerable<Comic> comics) {
            var modify = comics.ToList();

            var removed = modify.Select(this.GetStored).ToList();

            this.RemoveComics(modify);
            this.AddComics(modify);

            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, add: modify, remove: removed));
        }

        public void Refresh(IEnumerable<Comic> comics) {
            this.RefreshComics(comics);
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.Refresh));
        }

        public override bool Contains(Comic comic) => this.comics.ContainsKey(comic.UniqueIdentifier);
        public override Comic GetStored(Comic comic) => this.comics[comic.UniqueIdentifier];
        public override int Count() => this.comics.Count;
        public override IEnumerator<Comic> GetEnumerator() => this.comics.Values.GetEnumerator();
    }
}
