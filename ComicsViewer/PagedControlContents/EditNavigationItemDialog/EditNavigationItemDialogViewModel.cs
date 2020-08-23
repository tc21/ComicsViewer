using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    // really a RenameTagHelperClass for now...
    public class EditNavigationItemDialogViewModel {
        public string ItemTitle { get; }

        private NavigationTag NavigationTag => this.parent.NavigationTag;
        private readonly ComicNavigationItemGridViewModel parent;

        public EditNavigationItemDialogViewModel(ComicNavigationItemGridViewModel parent, string propertyName) {
            this.parent = parent;
            this.ItemTitle = propertyName;
        }

        public Task Save(string newItemTitle) {
            return this.parent.MainViewModel.RenameTagAsync(this.ItemTitle, newItemTitle);
        }

        // returns null if title is valid.
        public ValidateResult GetItemTitleInvalidReason(string title) {
            switch (this.NavigationTag) {
                case NavigationTag.Tags:
                    if (title.Contains(",")) {
                        return "Tag name cannot contain commas.";
                    }

                    if (this.parent.ComicProperties.ContainsProperty(title)) {
                        return "A tag with the same name already exists. (In the future, we will support merging tags.)";
                    }

                    break;

                default:
                    throw new ProgrammerError("Editing properties other than tags not yet supported");
            }

            if (title.Trim() == "") {
                return "Tag name cannot be empty.";
            }

            return ValidateResult.Ok();
        }
    }
}
