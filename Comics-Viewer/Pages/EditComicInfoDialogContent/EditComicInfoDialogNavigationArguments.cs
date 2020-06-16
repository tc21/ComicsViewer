using ComicsViewer.ViewModels;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer.Pages {
    public class EditComicInfoDialogNavigationArguments {
        public ComicItemGridViewModel ParentViewModel { get; }
        public ComicItem ComicItem { get; }
        public ContentDialog ContainerDialog { get; }

        public EditComicInfoDialogNavigationArguments(ComicItemGridViewModel parentViewModel, ComicItem comicItem, ContentDialog containerDialog) {
            this.ParentViewModel = parentViewModel;
            this.ComicItem = comicItem;
            this.ContainerDialog = containerDialog;
        }
    }
}
