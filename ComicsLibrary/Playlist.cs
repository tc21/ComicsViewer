#nullable enable

using System.Collections.Generic;
using ComicsLibrary.Collections;

namespace ComicsLibrary {
    public class Playlist : ComicProperty {
        public Playlist(string name, IEnumerable<Comic> comics) : base(name, comics) { }
    }
}
