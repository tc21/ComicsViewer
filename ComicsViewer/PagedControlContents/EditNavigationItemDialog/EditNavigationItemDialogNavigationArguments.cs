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
        public ComicNavigationItemGridViewModel ParentViewModel { get; }
        public string PropertyName { get; }

        public EditNavigationItemDialogNavigationArguments(ComicNavigationItemGridViewModel parent, string propertyName) {
            this.ParentViewModel = parent;
            this.PropertyName = propertyName;
        }
    }
}
