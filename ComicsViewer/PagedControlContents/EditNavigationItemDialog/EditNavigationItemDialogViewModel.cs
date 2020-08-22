using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
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

        private readonly NavigationTag navigationTag;

        public EditNavigationItemDialogViewModel(ComicItemGridViewModel parent, NavigationTag navigationTag, string propertyName) {
            this.navigationTag = navigationTag;
            this.ItemTitle = propertyName;
        }

        public void TrySetNewItemTitle(string newTitle) {
            newTitle = newTitle.Trim();

            if (this.GetItemTitleInvalidReason(newTitle) is string reason) {
                this.InputInvalidReason = reason;
                this.InputIsValid = false;
            } else {
                this.newItemTitle = newTitle;
                this.InputIsValid = true;
            }
        }
        
        public void Save() {
            // TODO;
        }

        // returns null if title is valid.
        private string? GetItemTitleInvalidReason(string title) {
            switch (this.navigationTag) {
                case NavigationTag.Tags:
                    if (title.Contains(",")) {
                        return "Tag name cannot contain commas.";
                    }
                    break;
            }

            if (title.Trim() == "") {
                return "Tag name cannot be empty.";
            }

            return null;
        }
    }
}
