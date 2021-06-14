using ComicsLibrary;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Media.Imaging;
using System.Threading.Tasks;
using ComicsViewer.Common;

#nullable enable

namespace ComicsViewer.ViewModels {
    public abstract class ComicItem : ViewModelBase {
        public abstract string Title { get; }
        public abstract string Subtitle { get; }
        public abstract bool IsLoved { get; }

        public abstract IEnumerable<Comic> ContainedComics();

        public Uri? ThumbnailImageSource { get; protected set; }

        private static readonly Uri placeholderThumbnailImageSource = new("ms-appx:///Assets/comics-px-padded.png");

        protected async Task RefreshImageSourceAsync() {
            // UWP is smart enough to not reload an image if the new source is the same (of course with no way to override that).
            // So we have to set a placeholder.
            var original = this.ThumbnailImageSource;

            this.ThumbnailImageSource = placeholderThumbnailImageSource;
            this.OnPropertyChanged(nameof(this.ThumbnailImageSource));

            await Task.Delay(100);

            this.ThumbnailImageSource = original;
            this.OnPropertyChanged(nameof(this.ThumbnailImageSource));
        }
    }
}