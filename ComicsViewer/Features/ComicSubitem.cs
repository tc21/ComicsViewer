using System.Collections.Generic;
using System.Linq;
using ComicsLibrary;

#nullable enable

namespace ComicsViewer.Features {
    public class ComicSubitem {
        public Comic Comic { get; }
        public string RootPath { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> Files { get; }

        public ComicSubitem(Comic comic, string displayName, string root, IEnumerable<string> files) {
            this.Comic = comic;
            this.RootPath = root;
            this.DisplayName = displayName;
            this.Files = files.ToList();
        }
    }
}