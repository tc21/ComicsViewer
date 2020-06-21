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
            if (title != this.ComicTitle) {
                this.Comic.Metadata.DisplayTitle = title.Trim();
            }

            if (tags != this.ComicTags) {
                this.Comic.Metadata.Tags.Clear();
                foreach (var tag in tags.Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                    this.Comic.Metadata.Tags.Add(tag.Trim());
                }
            }

            if (loved != this.ComicLoved) {
                this.Comic.Metadata.Loved = loved;
            }

            if (disliked != this.ComicDisliked) {
                this.Comic.Metadata.Disliked = disliked;
            }

            // we don't care about what happens after this, the program works even if you don't await this,
            // but it's probably best practice to do so anyway
            await this.MainViewModel.NotifyComicsChangedAsync(new[] { this.Comic });
        }
    }
}
