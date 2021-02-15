using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class EditComicInfoDialogNavigationArguments {
        public MainViewModel MainViewModel { get; }
        public ComicWorkItem ComicItem { get; }

        public EditComicInfoDialogNavigationArguments(MainViewModel mainViewModel, ComicWorkItem comicItem) {
            this.MainViewModel = mainViewModel;
            this.ComicItem = comicItem;
        }
    }
}
