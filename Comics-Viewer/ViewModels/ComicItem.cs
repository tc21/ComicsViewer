using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels {
    public abstract class ComicItem {
        public abstract string ThumbnailPath { get; }
        public abstract string Title { get; }
        public abstract string Subtitle { get; }
    }
}
