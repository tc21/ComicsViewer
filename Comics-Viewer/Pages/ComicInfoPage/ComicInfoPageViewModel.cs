using ComicsLibrary;
using ComicsViewer.Profiles;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Pages {
    public class ComicInfoPageViewModel : ViewModelBase {
        internal readonly ComicItemGridViewModel ParentViewModel;
        internal readonly ComicItem Item;
        private MainViewModel MainViewModel => ParentViewModel.MainViewModel;

        public ComicInfoPageViewModel(ComicItemGridViewModel parentViewModel, ComicItem item) {
            if (item.ItemType != ComicItemType.Work) {
                throw new ApplicationLogicException();
            }

            this.ParentViewModel = parentViewModel;
            this.Item = item;
        }


        public async Task Initialize() {
            foreach (var item in await this.GetSubitems(this.Item.TitleComic)) {
                this.ComicSubitems.Add(item);
            }

            this.IsLoadingSubItems = false;
            this.OnPropertyChanged(nameof(this.IsLoadingSubItems));
        }

        #region Open Comic - Subitems

        public readonly ObservableCollection<ComicSubitem> ComicSubitems = new ObservableCollection<ComicSubitem>();
        public bool IsLoadingSubItems { get; private set; } = true;

        public Task OpenItem(ComicSubitem item) {
            return Startup.OpenComicAtPathAsync(item.FullPath, this.MainViewModel.Profile);
        }

        // Temporary: this code should be moved elsewhere
        // Unfortunately we aren't actually on .NET Core 3.0, meaning we can't await an IAsyncEnumerable
        private async Task<IEnumerable<ComicSubitem>> GetSubitems(Comic comic) {
            // We currently recurse one level. More levels may be desired in the future...
            var subitems = new List<ComicSubitem>();

            var rootItem = await this.ComicSubitemForFolder(comic, await StorageFolder.GetFolderFromPathAsync(comic.Path), rootItem: true);
            if (rootItem != null) {
                subitems.Add(rootItem);
            }

            var folder = await StorageFolder.GetFolderFromPathAsync(comic.Path);
            foreach (var subfolder in await folder.GetFoldersAsync()) {
                var item = await this.ComicSubitemForFolder(comic, subfolder);
                if (item != null) {
                    subitems.Add(item);
                }
            }

            return subitems;
        }

        private async Task<ComicSubitem?> ComicSubitemForFolder(Comic comic, StorageFolder folder, bool rootItem = false) {
            var files = await this.MainViewModel.Profile.GetFilesForComicFolderAsync(folder);
            var fileCount = files.Count();

            if (fileCount == 0) {
                return null;
            }

            if (rootItem) {
                return new ComicSubitem(comic, relativePath: "", displayName: "(root item)", fileCount);
            }

            return new ComicSubitem(comic, relativePath: folder.Name, displayName: folder.Name, fileCount);
        }

        #endregion

        private Comic Comic => this.Item.TitleComic;

        public string ComicTitle => this.Comic.DisplayTitle;
        public string ComicAuthor => this.Comic.DisplayAuthor;
        public string ComicTags => string.Join(", ", this.Comic.Tags);
        public bool ComicLoved => this.Comic.Loved;
        public bool ComicDisliked => this.Comic.Disliked;

        /* Category editing is currently disabled */
        public string ComicCategory => this.Comic.DisplayCategory;

        public void RefreshComicInfo() {
            this.OnPropertyChanged(nameof(this.ComicTitle));
            this.OnPropertyChanged(nameof(this.ComicAuthor));
            this.OnPropertyChanged(nameof(this.ComicTags));
            this.OnPropertyChanged(nameof(this.ComicLoved));
            this.OnPropertyChanged(nameof(this.ComicDisliked));
        }

        public async Task SaveComicInfoAsync(string title, string author, string tags, bool loved, bool disliked) {
            if (title != this.ComicTitle) {
                this.Comic.Metadata.DisplayTitle = title.Trim();
            }

            if (author != this.ComicAuthor) {
                this.Comic.Metadata.DisplayAuthor = author.Trim();
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

            this.RefreshComicInfo();

            // we don't care about what happens after this, the program works even if you don't await this,
            // but it's probably best practice to do so anyway
            await this.MainViewModel.NotifyComicsChangedAsync(new[] { this.Comic });
        }
    }

    public class ComicSubitem {
        private readonly Comic comic;
        private readonly string relativePath;
        private readonly int itemCount;
        private readonly string displayName;

        public string DisplayName => $"{this.displayName} ({this.itemCount} items)";
        public string FullPath => Path.Combine(comic.Path, this.relativePath);

        public ComicSubitem(Comic comic, string relativePath, string displayName, int itemCount) {
            this.comic = comic;
            this.relativePath = relativePath;
            this.displayName = displayName;
            this.itemCount = itemCount;
        }
    }
}
