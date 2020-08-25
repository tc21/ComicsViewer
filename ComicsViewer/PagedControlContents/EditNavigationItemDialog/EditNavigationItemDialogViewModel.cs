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
            if (newItemTitle == this.ItemTitle) {
                return Task.CompletedTask;
            }

            return this.parent.MainViewModel.UpdateTagName(this.ItemTitle, newItemTitle);
        }

        // returns null if title is valid.
        public ValidateResult GetItemTitleInvalidReason(string title) {
            if (title == this.ItemTitle) {
                return ValidateResult.Ok();
            }

            switch (this.NavigationTag) {
                case NavigationTag.Tags:
                    if (title.Contains(",")) {
                        return "Tag name cannot contain commas.";
                    }

                    break;

                default:
                    throw new ProgrammerError("Editing properties other than tags not yet supported");
            }

            if (title.Trim() == "") {
                return "Tag name cannot be empty.";
            }

            return ValidateResult.Ok($"Warning: if the tag '{title}' already exists, the two tags will be merged. This cannot be undone.");
        }
    }
}
