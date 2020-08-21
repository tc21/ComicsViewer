using ComicsLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class EditComicInfoDialogViewModel : ViewModelBase {
        public readonly ComicItem Item;
        public readonly ComicItemGridViewModel ParentViewModel;
        private MainViewModel MainViewModel => ParentViewModel.MainViewModel;

        public EditComicInfoDialogViewModel(ComicItemGridViewModel parentViewModel, ComicItem item) {
            this.ParentViewModel = parentViewModel;
            this.Item = item;
        }

        private Comic Comic => this.Item.TitleComic;

        public string ComicTitle => this.Comic.DisplayTitle;
        public string ComicAuthor => this.Comic.DisplayAuthor;
        public string ComicTags => string.Join(", ", this.Comic.Tags);
        public bool ComicLoved => this.Comic.Loved;
        public bool ComicDisliked => this.Comic.Disliked;

        /* Category editing is currently disabled */
        public string ComicCategory => this.Comic.DisplayCategory;

        public async Task SaveComicInfoAsync(string title, string tags, bool loved, bool disliked) {
            var modified = this.Comic.WithUpdatedMetadata(metadata => {
                if (title != this.ComicTitle) {
                    metadata.DisplayTitle = title.Trim();
                }

                if (tags != this.ComicTags) {
                    metadata.Tags = tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(tag => tag.Trim()).ToHashSet();
                }

                if (loved != this.ComicLoved) {
                    metadata.Loved = loved;
                }

                if (disliked != this.ComicDisliked) {
                    metadata.Disliked = disliked;
                }

                return metadata;
            });

            // we don't care about what happens after this, the program works even if you don't await this,
            // but it's probably best practice to do so anyway
            await this.MainViewModel.UpdateComicAsync(new[] { modified });
        }
    }
}
