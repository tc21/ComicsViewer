using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class EditComicInfoDialogNavigationArguments {
        public ComicItemGridViewModel ParentViewModel { get; }
        public ComicItem ComicItem { get; }

        public EditComicInfoDialogNavigationArguments(ComicItemGridViewModel parentViewModel, ComicItem comicItem) {
            this.ParentViewModel = parentViewModel;
            this.ComicItem = comicItem;
        }
    }
}
