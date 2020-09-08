using ComicsLibrary;

#nullable enable 

namespace ComicsViewer.Features {
    public class ComicSubitem {
        private readonly Comic comic;
        private readonly string relativePath;
        private readonly int itemCount;
        private readonly string displayName;

        public string DisplayName => $"{this.displayName} ({this.itemCount} items)";
        public string Path => System.IO.Path.Combine(this.comic.Path, this.relativePath);

        public ComicSubitem(Comic comic, string relativePath, string displayName, int itemCount) {
            this.comic = comic;
            this.relativePath = relativePath;
            this.displayName = displayName;
            this.itemCount = itemCount;
        }
    }
}