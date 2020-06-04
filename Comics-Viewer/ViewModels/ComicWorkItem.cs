using ComicsLibrary;
using ComicsViewer.Profiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicWorkItem : ComicItem {
        public Comic Comic { get; }

        public override string ThumbnailPath => Path.Combine(Defaults.ThumbnailFolderPath, this.Comic.UniqueIdentifier + ".thumbnail.jpg");
        public override string Title => this.Comic.DisplayTitle;
        public override string Subtitle => this.Comic.DisplayAuthor;

        public ComicWorkItem(Comic comic) {
            this.Comic = comic;
        }
    }
}
