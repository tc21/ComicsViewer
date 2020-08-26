using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.IO;
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

        public Task SaveAsync(string newItemTitle) {
            if (newItemTitle == this.ItemTitle) {
                return Task.CompletedTask;
            }

            return this.NavigationTag switch {
                NavigationTag.Tags => this.parent.MainViewModel.UpdateTagNameAsync(this.ItemTitle, newItemTitle),
                NavigationTag.Author => this.parent.MainViewModel.StartRenameAuthorTaskAsync(this.ItemTitle, newItemTitle),
                _ => throw new ProgrammerError("unhandled switch case"),
            };
        }

        // returns null if title is valid.
        public ValidateResult GetItemTitleInvalidReason(string title) {
            if (title == this.ItemTitle) {
                return ValidateResult.Ok();
            }

            if (title.Trim() == "") {
                return $"{this.NavigationTag.Describe(capitalized: true)} name cannot be empty.";
            }

            switch (this.NavigationTag) {  // switch NavigationTag
                case NavigationTag.Tags:
                    if (title.Contains(",")) {
                        return "Tag name cannot contain commas.";
                    }

                    return ValidateResult.Ok($"Warning: if the tag '{title}' already exists, the two tags will be merged. This cannot be undone.");

                case NavigationTag.Author:
                    if (Path.GetInvalidFileNameChars().Any(c => title.Contains(c))) {
                        return $"Author names cannot contain characters that are not valid in file names. ({string.Join("", Path.GetInvalidFileNameChars())})";
                    }

                    return ValidateResult.Ok("Warning: renaming authors will change move the files representing the comic to a new folder. " +
                        "If the author already exists, the two authors will be merged. This cannot be undone.");

                default:
                    throw new ProgrammerError("Editing properties other than tags not yet supported");
            }

        }
    }
}
