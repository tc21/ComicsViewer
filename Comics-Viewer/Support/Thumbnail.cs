using ComicsLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Thumbnails {
    public static class Thumbnail {
        // TODO this class in unused
        public static string ThumbnailPath(Comic comic) {
            return Path.Combine(Defaults.ThumbnailFolderPath, comic.UniqueIdentifier + ".thumbnail.jpg");
        }
    }
}
