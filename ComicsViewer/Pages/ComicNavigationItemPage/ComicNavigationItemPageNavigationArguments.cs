using ComicsLibrary.Collections;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class ComicNavigationItemPageNavigationArguments {
        public MainViewModel MainViewModel { get; set; }
        public NavigationTag NavigationTag { get; set; }
        public ComicNavigationItem ComicItem { get; set; }
        public ComicItemGridViewModelProperties? Properties { get; set; }

        public ComicNavigationItemPageNavigationArguments(
            MainViewModel mainViewModel, 
            NavigationTag navigationTag, 
            ComicNavigationItem comicItem, 
            ComicItemGridViewModelProperties? properties = null
        ) {
            this.MainViewModel = mainViewModel;
            this.NavigationTag = navigationTag;
            this.ComicItem = comicItem;
            this.Properties = properties;
        }
    }
}