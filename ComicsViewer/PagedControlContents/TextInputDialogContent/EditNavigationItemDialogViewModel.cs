using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Support;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class EditNavigationItemDialogViewModel {
        private string ItemTitle { get; }

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

            return this.NavigationTag switch {  // switch NavigationTag
                NavigationTag.Tags => this.parent.MainViewModel.UpdateTagNameAsync(this.ItemTitle, newItemTitle),
                NavigationTag.Author => this.parent.MainViewModel.StartRenameAuthorTaskAsync(this.ItemTitle, newItemTitle),
                NavigationTag.Category => this.parent.MainViewModel.RenameCategoryAsync(this.ItemTitle, newItemTitle),
                NavigationTag.Playlist => this.parent.MainViewModel.RenamePlaylistAsync(this.ItemTitle, newItemTitle),
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

            if (this.NavigationTag.RefersToFileName() && !title.IsValidFileName()) {
                return $"{this.NavigationTag.Describe(capitalized: true)} names cannot contain characters that are not valid in filesnames. " +
                    $"({string.Join("", Path.GetInvalidFileNameChars())})";
            }

            switch (this.NavigationTag) {  // switch NavigationTag
                case NavigationTag.Tags:
                    if (title.Contains(",")) {
                        return "Tag name cannot contain commas.";
                    }

                    return ValidateResult.Ok($"Warning: if the tag '{title}' already exists, the two tags will be merged. This cannot be undone.");

                case NavigationTag.Author:
                    if (title == ComicsLoader.UnknownAuthorName) {
                        return "This author name is not available. It is reserved by the application.";
                    }

                    return ValidateResult.Ok("Warning: renaming authors will change move the files representing the comic to a new folder. " +
                        "If the author already exists, the two authors will be merged. This cannot be undone.");

                case NavigationTag.Category:
                    if (title == ComicsLoader.UnknownCategoryName) {
                        return "This category name is not available. It is reserved by the application.";
                    }

                    if (this.parent.MainViewModel.Profile.RootPaths.ContainsName(title)) {
                        return $"The category '{title}' already exists. You cannot rename a category to one that already exists. " +
                            $"To merge categories, right click a category and select 'Move'.";
                    }

                    return ValidateResult.Ok();

                case NavigationTag.Playlist:
                    if (this.parent.MainViewModel.Playlists.ContainsKey(title)) {
                        return $"Playlist '{title}' already exists";
                    }

                    return ValidateResult.Ok();

                default:
                    throw new ProgrammerError($"Editing this property ({this.NavigationTag.Describe()}) is not yet supported");
            }

        }
    }
}
