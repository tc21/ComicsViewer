using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class EditNavigationItemDialogNavigationArguments {
        public ComicNavigationItemGridViewModel ParentViewModel { get; }
        public string PropertyName { get; }

        public EditNavigationItemDialogNavigationArguments(ComicNavigationItemGridViewModel parent, string propertyName) {
            this.ParentViewModel = parent;
            this.PropertyName = propertyName;
        }
    }
}
