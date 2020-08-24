using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class EditComicInfoDialogNavigationArguments {
        public ComicItemGridViewModel ParentViewModel { get; }
        public ComicWorkItem ComicItem { get; }

        public EditComicInfoDialogNavigationArguments(ComicItemGridViewModel parentViewModel, ComicWorkItem comicItem) {
            this.ParentViewModel = parentViewModel;
            this.ComicItem = comicItem;
        }
    }
}
