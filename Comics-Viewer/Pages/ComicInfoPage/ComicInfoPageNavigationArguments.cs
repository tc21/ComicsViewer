using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls.Primitives;

#nullable enable

namespace ComicsViewer.Pages {
    public class ComicInfoPageNavigationArguments {
        public ComicItemGridViewModel ParentViewModel { get; }
        public ComicItem ComicItem { get; }
        public FlyoutBase ParentFlyout { get; }

        public ComicInfoPageNavigationArguments(
                ComicItemGridViewModel parentViewModel, ComicItem comicItem, FlyoutBase parentFlyout, string? openToPane = null) {
            this.ParentViewModel = parentViewModel;
            this.ComicItem = comicItem;
            this.ParentFlyout = parentFlyout;
        }
    }
}
