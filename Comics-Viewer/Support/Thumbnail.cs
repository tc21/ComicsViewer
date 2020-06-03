using ComicsLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.Thumbnails {
    static class Thumbnail {
        internal static string ThumbnailPath(Comic comic) {
            return Path.Combine(Defaults.ThumbnailFolderPath, comic.UniqueIdentifier + ".thumbnail.jpg");
        }
    }
}
