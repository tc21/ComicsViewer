using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicWorkItemPageViewModel : ViewModelBase, IDisposable {
        public MainViewModel MainViewModel { get; }
        public ComicWorkItem ComicItem { get; }

        public ComicWorkItemPageViewModel(MainViewModel mainViewModel, ComicWorkItem comicItem) {
            this.MainViewModel = mainViewModel;
            this.ComicItem = comicItem;

            this.MainViewModel.ProfileChanged += this.MainViewModel_ProfileChanged;
            this.ComicItem.RequestingRefresh += this.ComicItem_RequestingRefresh;
        }

        public ObservableCollection<ComicSubitemContainer> Subitems = new();

        public int ImageWidth => this.MainViewModel.Profile.ImageWidth;
        public int ImageHeight => this.MainViewModel.Profile.ImageHeight;

        public int HighlightImageWidth => this.ImageWidth / 2;
        public int HighlightImageHeight => this.ImageHeight / 2;

        public bool Initialized {
            get => this._initialized;
            private set {
                if (this._initialized == value) {
                    return;
                }

                this._initialized = value;
                this.OnPropertyChanged();
            }
        }

        private bool _initialized = false;

        private ComicSubitem? _primarySubitem;
        private ComicSubitem PrimarySubitem => this.Initialized
            ? this._primarySubitem ?? throw ProgrammerError.Unwrapped()
            : throw new ProgrammerError("Attempting to access properties of an uninitialized ComicWorkItemPageViewModel");

        public async Task InitializeAsync() {
            var subitems = (await this.MainViewModel.Profile.GetComicSubitemsAsync(this.ComicItem.Comic))?.ToList();

            if (subitems is null || subitems.Count == 0) {
                return;
            }

            this._primarySubitem = subitems[0];
            this.Subitems.AddRange(subitems.Skip(1).Select(subitem => new ComicSubitemContainer(subitem)));

            this.Initialized = true;
        }

        public async Task LoadThumbnailsAsync() {
            foreach (var subitem in this.Subitems) {
                await subitem.InitializeAsync(decodePixelHeight: this.MainViewModel.Profile.ImageHeight);
            }
        }

        public Task OpenComicItemAsync(ComicSubitem? subitem = null) {
            return Startup.OpenComicSubitemAsync(subitem ?? this.PrimarySubitem, this.MainViewModel.Profile);
        }

        private void MainViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            this.OnPropertyChanged(nameof(this.ImageHeight));
            this.OnPropertyChanged(nameof(this.ImageWidth));
            this.OnPropertyChanged(nameof(this.HighlightImageHeight));
            this.OnPropertyChanged(nameof(this.HighlightImageWidth));
        }

        public Task ToggleLovedStatus() {
            var newStatus = !this.ComicItem.Comic.Loved;
            var modified = this.ComicItem.Comic.WithMetadata(loved: newStatus);

            return this.MainViewModel.UpdateComicAsync(new[] { modified });
        }

        /// <returns>true if the comic has descriptions, false if it doesn't</returns>
        public async Task<bool> TryLoadDescriptionsAsync(TextBlock infoTextBlock) {
            StorageFolder comicFolder;
            try {
                comicFolder = await StorageFolder.GetFolderFromPathAsync(this.ComicItem.Comic.Path);
            } catch (FileNotFoundException) {
                return false;
            } catch (UnauthorizedAccessException) {
                return false;
            }

            var descriptionAdded = false;

            foreach (var descriptionSpecification in this.MainViewModel.Profile.ExternalDescriptions) {
                if (await descriptionSpecification.FetchFromFolderAsync(comicFolder) is not { } description) {
                    continue;
                }

                descriptionAdded = true;

                var text = new Run { Text = description.Content };

                switch (description.DescriptionType) {
                    case ExternalDescriptionType.Text:
                        infoTextBlock.Inlines.Add(text);
                        break;
                    case ExternalDescriptionType.Link:
                        var link = new Hyperlink();

                        try {
                            link.NavigateUri = new Uri(description.Content);
                        } catch (UriFormatException) {
                            // do nothing
                        }

                        link.Inlines.Add(text);
                        infoTextBlock.Inlines.Add(link);
                        break;
                    default:
                        throw new ProgrammerError("Unhandled switch case");
                }

                infoTextBlock.Inlines.Add(new LineBreak());
            }

            return descriptionAdded;
        }

        private void ComicItem_RequestingRefresh(ComicWorkItem sender, ComicWorkItem.RequestingRefreshType type) {
            // TODO
        }

        public void Dispose() {
            this.MainViewModel.ProfileChanged -= this.MainViewModel_ProfileChanged;
            this.ComicItem.RequestingRefresh -= this.ComicItem_RequestingRefresh;
        }
    }
}
