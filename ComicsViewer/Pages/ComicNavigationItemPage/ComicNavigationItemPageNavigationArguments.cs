using ComicsLibrary.Collections;
using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class ComicNavigationItemPageNavigationArguments {
        public MainViewModel MainViewModel { get; set; }
        public NavigationTag NavigationTag { get; set; }
        public ComicView Comics { get; set; }
        public ComicItemGridViewModelProperties? Properties { get; set; }

        public ComicNavigationItemPageNavigationArguments(MainViewModel mainViewModel, NavigationTag navigationTag, ComicView comics, ComicItemGridViewModelProperties? properties) {
            this.MainViewModel = mainViewModel;
            this.NavigationTag = navigationTag;
            this.Comics = comics;
            this.Properties = properties;
        }
    }
}