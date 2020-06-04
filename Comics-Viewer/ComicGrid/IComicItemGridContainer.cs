using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ComicGrid {
    interface IComicItemGridContainer {
        ComicItemGrid? Grid { get; }
    }
}
