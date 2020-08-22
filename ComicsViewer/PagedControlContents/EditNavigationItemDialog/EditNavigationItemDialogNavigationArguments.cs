using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Pages {
    public class EditNavigationItemDialogNavigationArguments {
        public ComicItemGridViewModel ParentViewModel { get; }
        public NavigationTag NavigationTag { get; }
        public string PropertyName { get; }

        public EditNavigationItemDialogNavigationArguments(ComicItemGridViewModel parent, NavigationTag navigationTag, string propertyName) {
            this.ParentViewModel = parent;
            this.NavigationTag = navigationTag;
            this.PropertyName = propertyName;
        }
    }
}
