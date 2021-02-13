using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class ComicRootPageNavigationArguments {
        public MainViewModel MainViewModel { get; set; }
        public NavigationTag NavigationTag { get; set; }

        public ComicRootPageNavigationArguments(MainViewModel mainViewModel, NavigationTag navigationTag) {
            this.MainViewModel = mainViewModel;
            this.NavigationTag = navigationTag;
        }
    }
}