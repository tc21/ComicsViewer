#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ComicsLibrary.Collections;

namespace ComicsLibrary {
    public class Playlist : FilteredComicView, IComicProperty {
        public string Name { get; }
        public IEnumerable<Comic> Comics => this;

        private readonly HashSet<string> uniqueIds = new HashSet<string>();

        private Playlist(ComicView parent, string name, HashSet<string> uniqueIds) : base(parent, comic => uniqueIds.Contains(comic.UniqueIdentifier)) {
            this.uniqueIds = uniqueIds;
            this.Name = name;
        }

        public static Playlist Make(ComicView parent, string name, IEnumerable<string> uniqueIds) {
            return new Playlist(parent, name, new HashSet<string>(uniqueIds));
        }

        public void Add(IEnumerable<Comic> comics) {
            comics = comics.ToList();

            this.uniqueIds.UnionWith(comics.Select(comic => comic.UniqueIdentifier));
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, add: comics));
        }

        public void Remove(IEnumerable<Comic> comics) {
            comics = comics.ToList();

            this.uniqueIds.ExceptWith(comics.Select(comic => comic.UniqueIdentifier));
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, remove: comics));
        }
    }
}
