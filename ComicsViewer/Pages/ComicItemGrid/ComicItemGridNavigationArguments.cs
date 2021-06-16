using System;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public class ComicItemGridNavigationArguments {
        public ComicItemGridViewModel? ViewModel { get; set; }
        public ComicItem? HighlightedComicItem { get; set; }
        public Action<ComicItemGrid, NavigationEventArgs>? OnNavigatedTo { get; set; }
    }
}
