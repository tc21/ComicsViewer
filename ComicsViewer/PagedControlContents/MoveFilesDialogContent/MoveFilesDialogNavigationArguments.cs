using ComicsLibrary;
using ComicsViewer.ViewModels.Pages;
using System.Collections.Generic;

#nullable enable

namespace ComicsViewer.Pages {
    public class MoveFilesDialogNavigationArguments {
        public ComicItemGridViewModel ParentViewModel { get; }
        public IEnumerable<Comic> Comics { get; }

        public MoveFilesDialogNavigationArguments(ComicItemGridViewModel parentViewModel, IEnumerable<Comic> comics) {
            this.ParentViewModel = parentViewModel;
            this.Comics = comics;
        }
    }
}
