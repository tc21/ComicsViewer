using ComicsLibrary;
using ComicsViewer.Features;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicInfoFlyoutViewModel : ViewModelBase {
        internal readonly ComicItemGridViewModel ParentViewModel;
        internal readonly ComicItem Item;
        private MainViewModel MainViewModel => ParentViewModel.MainViewModel;

        public ComicInfoFlyoutViewModel(ComicItemGridViewModel parentViewModel, ComicItem item) {
            if (item.ItemType != ComicItemType.Work) {
                throw new ApplicationLogicException("ComicInfoFlyoutViewModel can only be created for work item");
            }

            this.ParentViewModel = parentViewModel;
            this.Item = item;
        }


        public async Task InitializeAsync() {
            try {
                foreach (var item in await this.GetSubitemsAsync(this.Item.TitleComic)) {
                    this.ComicSubitems.Add(item);
                }
            } catch (UnauthorizedAccessException) {
                await ExpectedExceptions.UnauthorizedFileSystemAccessAsync();
            } catch (FileNotFoundException) {
                await ExpectedExceptions.FileNotFoundAsync();
            }

            this.IsLoadingSubItems = false;
            this.OnPropertyChanged(nameof(this.IsLoadingSubItems));
        }

        #region Open Comic - Subitems

        public readonly ObservableCollection<ComicSubitem> ComicSubitems = new ObservableCollection<ComicSubitem>();
        public bool IsLoadingSubItems { get; private set; } = true;

        public Task OpenItemAsync(ComicSubitem item) {
            return Startup.OpenComicAtPathAsync(item.FullPath, this.MainViewModel.Profile);
        }

        // Temporary: this code should be moved elsewhere
        // Unfortunately we aren't actually on .NET Core 3.0, meaning we can't await an IAsyncEnumerable
        private async Task<IEnumerable<ComicSubitem>> GetSubitemsAsync(Comic comic) {
            // We currently recurse one level. More levels may be desired in the future...
            var subitems = new List<ComicSubitem>();

            var rootItem = await this.ComicSubitemForFolderAsync(comic, await StorageFolder.GetFolderFromPathAsync(comic.Path), rootItem: true);
            if (rootItem != null) {
                subitems.Add(rootItem);
            }

            var folder = await StorageFolder.GetFolderFromPathAsync(comic.Path);
            foreach (var subfolder in await folder.GetFoldersAsync()) {
                var item = await this.ComicSubitemForFolderAsync(comic, subfolder);
                if (item != null) {
                    subitems.Add(item);
                }
            }

            return subitems;
        }

        private async Task<ComicSubitem?> ComicSubitemForFolderAsync(Comic comic, StorageFolder folder, bool rootItem = false) {
            var files = await this.MainViewModel.Profile.GetTopLevelFilesForFolderAsync(folder);
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

        #region Externally loaded comic descriptions

        /// <returns>true if the comic has descriptions, false if it doesn't</returns>
        public async Task<bool> LoadDescriptionsAsync(TextBlock infoPivotText) {
            if (this.Item.ItemType != ComicItemType.Work) {
                return false;
            }

            var comicFolder = await StorageFolder.GetFolderFromPathAsync(this.Item.TitleComic.Path);
            var descriptionAdded = false;

            foreach (var descriptionSpecification in this.MainViewModel.Profile.ExternalDescriptions) {
                if ((await descriptionSpecification.FetchFromFolderAsync(comicFolder)) is ExternalDescription description) {
                    descriptionAdded = true;

                    var text = new Run { Text = description.Content };

                    switch (description.DescriptionType) {
                        case ExternalDescriptionType.Text:
                            infoPivotText.Inlines.Add(text);
                            break;
                        case ExternalDescriptionType.Link:
                            var link = new Hyperlink { NavigateUri = new Uri(description.Content) };
                            link.Inlines.Add(text);
                            infoPivotText.Inlines.Add(link);
                            break;
                        default:
                            throw new ApplicationLogicException("Unhandled switch case");
                    }

                    infoPivotText.Inlines.Add(new LineBreak());
                }
            }

            return descriptionAdded;
        }

        #endregion
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
