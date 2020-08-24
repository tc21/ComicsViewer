using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable

namespace ComicsViewer.ViewModels {
    public abstract class ComicItem : ViewModelBase, IDisposable {
        public abstract string Title { get; }
        public abstract string Subtitle { get; }
        public abstract bool IsLoved { get; }
        public abstract bool IsDisliked { get; }

        public abstract IEnumerable<Comic> ContainedComics();

        public BitmapImage? ThumbnailImage { get; protected set; }

        public static ComicWorkItem WorkItem(MainViewModel vm, Comic comic, ComicView trackChangesFrom) {
            return new ComicWorkItem(vm, comic, trackChangesFrom);
        }

        public static ComicNavigationItem NavigationItem(string name, ComicView comics) {
            return new ComicNavigationItem(name, comics);
        }

        public abstract void Dispose();
    }
}