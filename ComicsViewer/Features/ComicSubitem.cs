using System.Collections.Generic;
using System.Linq;
using ComicsLibrary;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Features {
    public class ComicSubitem {
        private readonly string displayName;
        private readonly List<string> files;

        public Comic Comic { get; }
        public string RootPath { get; }
        private int ItemCount => this.files.Count;
        // ReSharper disable once UnusedMember.Global
        // This is used via CallerMemberPath in ComicInfoFlyout
        public string DisplayName => $"{this.displayName} ({this.ItemCount} items)";
        public IEnumerable<string> Files => this.files;

        public ComicSubitem(Comic comic, string displayName, string root, IEnumerable<string> files) {
            this.Comic = comic;
            this.RootPath = root;
            this.displayName = displayName;
            this.files = files.ToList();
        }
    }
}