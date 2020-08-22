using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            this.AddComics(comics);
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, add: comics));
        }

        public void Remove(IEnumerable<Comic> comics) {
            this.RemoveComics(comics);
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, remove: comics));
        }

        // you should pass in the new list of comics
        public void Modify(IEnumerable<Comic> comics) {
            var removed = comics.Select(c => this.GetStored(c)).ToList();

            this.RemoveComics(comics);
            this.AddComics(comics);

            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, add: comics, remove: comics));
        }

        public void Refresh(IEnumerable<Comic> comics) {
            this.RefreshComics(comics);
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.Refresh));
        }

        public override bool Contains(Comic comic) => this.comics.ContainsKey(comic.UniqueIdentifier);
        public override Comic GetStored(Comic comic) => this.comics[comic.UniqueIdentifier];
        public override int Count() => this.comics.Count();
        public override IEnumerator<Comic> GetEnumerator() => this.comics.Values.GetEnumerator();
    }
}
