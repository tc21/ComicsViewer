using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    // really a RenameTagViewModel for now...
    public class EditNavigationItemDialogViewModel : ViewModelBase {
        private bool inputIsValid = false;
        public bool InputIsValid {
            get => this.inputIsValid;
            set {
                if (this.inputIsValid == value) {
                    return;
                }

                this.inputIsValid = value;
                this.OnPropertyChanged();
                this.OnPropertyChanged(nameof(this.InputIsNotValid));
            }
        }

        public bool InputIsNotValid => !this.InputIsValid;

        private string inputInvalidReason = "";
        public string InputInvalidReason {
            get => this.inputInvalidReason;
            set {
                if (this.inputInvalidReason == value) {
                    return;
                }

                this.inputInvalidReason = value;
                this.OnPropertyChanged();
            }
        }

        public string ItemTitle { get; }
        public string? newItemTitle;

        private NavigationTag NavigationTag => this.parent.NavigationTag;
        private readonly ComicNavigationItemGridViewModel parent;

        public EditNavigationItemDialogViewModel(ComicNavigationItemGridViewModel parent, string propertyName) {
            this.parent = parent;
            this.ItemTitle = propertyName;
        }

        public void TrySetNewItemTitle(string newTitle) {
            newTitle = newTitle.Trim();

            if (this.GetItemTitleInvalidReason(newTitle) is string reason) {
                this.InputInvalidReason = reason;
                this.newItemTitle = null;
                this.InputIsValid = false;
            } else {
                this.newItemTitle = newTitle;
                this.InputIsValid = true;
            }
        }
        
        public Task Save() {
            if (this.newItemTitle == null) {
                throw new ProgrammerError("should not allow Save() to be called unless new name is valid.");
            }

            return this.parent.MainViewModel.RenameTagAsync(this.ItemTitle, newItemTitle);
        }

        // returns null if title is valid.
        private string? GetItemTitleInvalidReason(string title) {
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

            return null;
        }
    }
}
