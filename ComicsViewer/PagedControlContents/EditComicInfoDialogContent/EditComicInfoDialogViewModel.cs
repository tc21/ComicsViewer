using ComicsLibrary;
using ComicsViewer.Support;
using System.Threading.Tasks;
using ComicsViewer.Common;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class EditComicInfoDialogViewModel : ViewModelBase {
        public readonly ComicWorkItem Item;
        public MainViewModel MainViewModel { get; }

        public EditComicInfoDialogViewModel(MainViewModel mainViewModel, ComicWorkItem item) {
            this.MainViewModel = mainViewModel;
            this.Item = item;
        }

        private Comic Comic => this.Item.Comic;

        public string ComicTitle => this.Comic.DisplayTitle;
        public string ComicAuthor => this.Comic.Author;
        public string ComicTags => string.Join(", ", this.Comic.Tags);
        public bool ComicLoved => this.Comic.Loved;

        /* Category editing is currently disabled */
        public string ComicCategory => this.Comic.Category;

        public async Task SaveComicInfoAsync(string title, string tags, bool loved) {
            var assignTags = tags == this.ComicTags ? null : StringConversions.CommaDelimitedList.Convert(tags);

            var modified = this.Comic.WithMetadata(
                displayTitle: title.Trim(),
                tags: assignTags,
                loved: loved
            );

            // we don't care about what happens after this, the program works even if you don't await this,
            // but it's probably best practice to do so anyway
            await this.MainViewModel.UpdateComicAsync(new[] { modified });
        }
    }
}
