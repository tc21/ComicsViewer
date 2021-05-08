using ComicsLibrary;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Media.Imaging;
using ComicsViewer.Common;

#nullable enable

namespace ComicsViewer.ViewModels {
    public abstract class ComicItem : ViewModelBase {
        public abstract string Title { get; }
        public abstract string Subtitle { get; }
        public abstract bool IsLoved { get; }

        public abstract IEnumerable<Comic> ContainedComics();

        public BitmapImage? ThumbnailImage { get; protected set; }
    }
}