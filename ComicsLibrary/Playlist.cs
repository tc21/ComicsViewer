#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace ComicsLibrary {
    public class Playlist {
        public string Name { get; }
        public List<Comic> Comics { get; }

        public Playlist(string name, IEnumerable<Comic> comics) {
            this.Name = name;
            this.Comics = comics.ToList();
        }
    }
}
