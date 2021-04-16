#nullable enable

using System.Collections.Generic;
using System.Linq;
using ComicsLibrary.Collections;

namespace ComicsLibrary {
    public class Playlist : FilteredComicView, IComicCollection {
        public string Name { get; }
        public ComicView Comics => this;

        private readonly HashSet<string> uniqueIds = new HashSet<string>();

        private Playlist(ComicView parent, string name, HashSet<string> uniqueIds) : base(parent, comic => uniqueIds.Contains(comic.UniqueIdentifier)) {
            this.uniqueIds = uniqueIds;
            this.Name = name;
        }

        public static Playlist Make(ComicView parent, string name, IEnumerable<string> uniqueIds) {
            return new Playlist(parent, name, new HashSet<string>(uniqueIds));
        }

        public void UnionWith(IEnumerable<Comic> comics) {
            comics = comics.Where(comic => !this.uniqueIds.Contains(comic.UniqueIdentifier)).ToList();

            this.uniqueIds.UnionWith(comics.Select(comic => comic.UniqueIdentifier));
            this.UpdateCache();
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, add: comics));
        }

        public void ExceptWith(IEnumerable<Comic> comics) {
            comics = comics.Where(comic => this.uniqueIds.Contains(comic.UniqueIdentifier)).ToList();

            this.uniqueIds.ExceptWith(comics.Select(comic => comic.UniqueIdentifier));
            this.UpdateCache();
            this.OnComicChanged(new ViewChangedEventArgs(ComicChangeType.ItemsChanged, remove: comics));
        }
    }
}
