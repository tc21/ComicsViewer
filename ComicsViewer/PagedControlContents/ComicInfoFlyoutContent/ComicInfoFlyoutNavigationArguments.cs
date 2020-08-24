using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;

#nullable enable

namespace ComicsViewer.Pages {
    public class ComicInfoFlyoutNavigationArguments {
        public ComicItemGridViewModel ParentViewModel { get; }
        public ComicWorkItem ComicItem { get; }
        public Action EditInfoCallback { get; }

        public ComicInfoFlyoutNavigationArguments(
                ComicItemGridViewModel parentViewModel, ComicWorkItem comicItem, Action editInfoCallback) {
            this.ParentViewModel = parentViewModel;
            this.ComicItem = comicItem;
            this.EditInfoCallback = editInfoCallback;
        }
    }
}
