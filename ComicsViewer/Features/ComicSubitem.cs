using System.Collections.Generic;
using System.Linq;
using ComicsLibrary;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Features {
    public class ComicSubitem {
        private readonly string displayName;
        private readonly List<StorageFile> files;

        public Comic Comic { get; }
        private int ItemCount => this.files.Count;
        public string DisplayName => $"{this.displayName} ({this.ItemCount} items)";
        public IReadOnlyList<StorageFile> Files => this.files;

        public ComicSubitem(Comic comic, string displayName, IEnumerable<StorageFile> files) {
            this.Comic = comic;
            this.displayName = displayName;
            this.files = files.ToList();
        }
    }
}