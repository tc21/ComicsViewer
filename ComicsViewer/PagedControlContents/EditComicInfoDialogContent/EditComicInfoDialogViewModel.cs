using ComicsLibrary;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class EditComicInfoDialogViewModel : ViewModelBase {
        public readonly ComicWorkItem Item;
        public readonly ComicItemGridViewModel ParentViewModel;
        private MainViewModel MainViewModel => ParentViewModel.MainViewModel;

        public EditComicInfoDialogViewModel(ComicItemGridViewModel parentViewModel, ComicWorkItem item) {
            this.ParentViewModel = parentViewModel;
            this.Item = item;
        }

        private Comic Comic => this.Item.Comic;

        public string ComicTitle => this.Comic.DisplayTitle;
        public string ComicAuthor => this.Comic.Author;
        public string ComicTags => string.Join(", ", this.Comic.Tags);
        public bool ComicLoved => this.Comic.Loved;
        public bool ComicDisliked => this.Comic.Disliked;

        /* Category editing is currently disabled */
        public string ComicCategory => this.Comic.Category;

        public async Task SaveComicInfoAsync(string title, string tags, bool loved, bool disliked) {
            var assignTags = (tags == this.ComicTags) ? null : StringConversions.CommaDelimitedList.Convert(tags);

            var modified = this.Comic.WithMetadata(
                displayTitle: title.Trim(),
                tags: assignTags,
                loved: loved,
                disliked: disliked
            );

            // we don't care about what happens after this, the program works even if you don't await this,
            // but it's probably best practice to do so anyway
            await this.MainViewModel.UpdateComicAsync(new[] { modified });
        }
    }
}
