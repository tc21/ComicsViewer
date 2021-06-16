using ComicsLibrary;
using ComicsViewer.Support;
using System.Threading.Tasks;
using ComicsViewer.Common;
using System.IO;
using ComicsViewer.ClassExtensions;
using Windows.UI.Xaml;
using System;
using System.Globalization;
using System.Linq;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class EditComicInfoDialogViewModel : ViewModelBase {
        public readonly ComicWorkItem Item;
        public MainViewModel MainViewModel { get; }

        public EditComicInfoDialogViewModel(MainViewModel mainViewModel, ComicWorkItem item) {
            this.MainViewModel = mainViewModel;
            this.Item = item;
        }

        public Comic Comic => this.Item.Comic;
        public string ComicTags => string.Join(", ", this.Comic.Tags);

        /// <summary>
        /// Saves the modified comic to disk, and to the application. 
        /// The viewmodel is no longer valid after this call. Do not attempt to use it further.
        /// </summary>
        public Task SaveComicInfoAsync(string displayTitle, string author, string category, string title, string dateAdded, string tags, bool loved) {
            var result = this.ValidateNewFileName(category, author, title).CombineWith(this.ValidateDateAdded(dateAdded));
            if (result.IsErr) {
                throw new ProgrammerError("Comic info must be verified to be valid before saving");
            }
            
            var assignTags = (tags == this.ComicTags)
                ? null 
                : StringConversions.CommaDelimitedList.Convert(tags);

            var metadata = new ComicMetadata {
                DateAdded = dateAdded,
                DisplayTitle = displayTitle.Trim(),
                Loved = loved,
                Tags = assignTags?.ToHashSet() ?? this.Comic.Tags.ToHashSet(),
                ThumbnailSource = this.Comic.ThumbnailSource
            };

            var path = this.SuggestedPath(category, author, title) ?? throw new ProgrammerError("Comic info must be verified to be valid before saving");
            var newComic = new Comic(path, title, author, category, metadata);

            return this.MainViewModel.StartMoveComicsTaskAsync(new[] { this.Comic }, new[] { newComic });
        }

        private string _warningText = "";
        public string WarningText {
            get => this._warningText;
            set {
                this._warningText = value;
                this.OnPropertyChanged();
            }
        }

        private Visibility _warningTextVisibility = Visibility.Visible;
        public Visibility WarningTextVisibility {
            get => this._warningTextVisibility;
            set {
                this._warningTextVisibility = value;
                this.OnPropertyChanged();
            }
        }

        private bool _canSave = true;
        public bool CanSave {
            get => this._canSave;
            set {
                this._canSave = value;
                this.OnPropertyChanged();
            }
        }

        private string? savedCategory;
        private string? savedAuthor;
        private string? savedTitle;

        public void UpdateIntendedChanges(string? category = null, string? author = null, string? title = null, string? dateAdded = null) {
            var result = this.ValidateNewFileName(category, author, title);

            if (dateAdded is not null) {
                result = result.CombineWith(this.ValidateDateAdded(dateAdded));
            }
            
            if (result.Comment is { } comment) {
                this.WarningText = comment;
                this.WarningTextVisibility = Visibility.Visible;
            } else {
                this.WarningTextVisibility = Visibility.Collapsed;
            }

            this.CanSave = result.IsOk;
        }

        private string? SuggestedPath(string category, string author, string title) {
            if (category == this.Comic.Category) {
                return Path.Combine(
                    Path.GetDirectoryName(Path.GetDirectoryName(this.Comic.Path)),
                    author,
                    title
                );
            }

            if (!this.MainViewModel.Profile.RootPaths.TryGetValue(category, out var namedPath)) {
                return null;
            }

            return Path.Combine(namedPath.Path, this.savedAuthor, this.savedTitle);
        }

        private ValidateResult ValidateNewFileName(string? category, string? author, string? title) {
            this.savedCategory = category ?? this.savedCategory ?? this.Comic.Category;
            this.savedAuthor = author ?? this.savedAuthor ?? this.Comic.Author;
            this.savedTitle = title ?? this.savedTitle ?? this.Comic.Title;

            if (this.savedCategory != this.Comic.Category && this.savedCategory == ComicsLoader.UnknownCategoryName) {
                return "This category name is not available. It is reserved by the application.";
            }

            if (this.savedAuthor != this.Comic.Author && this.savedAuthor == ComicsLoader.UnknownAuthorName) {
                return "This author name is not available. It is reserved by the application.";
            }

            if (!this.savedCategory.IsValidFileName()) {
                return $"Category cannot contain invalid filename characters ({string.Join("", Path.GetInvalidFileNameChars())}).";
            }

            if (!this.savedAuthor.IsValidFileName()) {
                return $"Author cannot contain invalid filename characters ({string.Join("", Path.GetInvalidFileNameChars())}).";
            }

            // Avoid collisions. Just ensuring the new path doesn't exist is insufficent.
            var newUniqueId = $"[{this.savedAuthor}]{this.savedTitle}";
            if (newUniqueId != this.Comic.UniqueIdentifier && this.MainViewModel.Comics.Contains(newUniqueId)) {
                return $"An item with this author and title already exists.";
            }

            if (this.SuggestedPath(this.savedCategory, this.savedAuthor, this.savedTitle) is not { } newPath) {
                return $"Category does not exist. The application does not yet support creating new categories here.";
            }

            if (newPath != this.Comic.Path && Uwp.Common.Win32Interop.IO.FileOrDirectoryExists(newPath)) {
                return $"An item already exists at {newPath}.";
            }

            return ValidateResult.Ok();
        }

        private ValidateResult ValidateDateAdded(string dateAdded) {
            if (DateTime.TryParseExact(dateAdded, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var _)) {
                return ValidateResult.Ok();
            } else {
                return "Could not parse Date added as date.";
            }
        }
    }
}
