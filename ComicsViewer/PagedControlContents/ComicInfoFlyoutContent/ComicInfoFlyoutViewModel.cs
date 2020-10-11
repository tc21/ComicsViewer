using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Support;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicInfoFlyoutViewModel : ViewModelBase {
        private readonly ComicItemGridViewModel parentViewModel;
        private readonly ComicWorkItem item;
        private MainViewModel MainViewModel => this.parentViewModel.MainViewModel;

        public ComicInfoFlyoutViewModel(ComicItemGridViewModel parentViewModel, ComicWorkItem item) {
            this.parentViewModel = parentViewModel;
            this.item = item;
        }


        public async Task InitializeAsync() {
            if (await this.MainViewModel.Profile.GetComicSubitemsAsync(this.item.Comic) is { } subitems) {
                this.ComicSubitems.AddRange(subitems);
            }

            this.IsLoadingSubItems = false;
            this.OnPropertyChanged(nameof(this.IsLoadingSubItems));
        }

        #region Open Comic - Subitems

        // ReSharper disable once CollectionNeverQueried.Global
        // Used via binding in ComicInfoFlyout
        public readonly ObservableCollection<ComicSubitem> ComicSubitems = new ObservableCollection<ComicSubitem>();
        public bool IsLoadingSubItems { get; private set; } = true;

        public Task OpenItemAsync(ComicSubitem item) {
            return Startup.OpenComicSubitemAsync(item, this.MainViewModel.Profile);
        }

        #endregion

        #region Externally loaded comic descriptions

        /// <returns>true if the comic has descriptions, false if it doesn't</returns>
        public async Task<bool> LoadDescriptionsAsync(TextBlock infoPivotText) {
            StorageFolder comicFolder;
            try {
                comicFolder = await StorageFolder.GetFolderFromPathAsync(this.item.Comic.Path);
            } catch (FileNotFoundException) {
                return false;
            } catch (UnauthorizedAccessException) {
                return false;
            }

            var descriptionAdded = false;

            foreach (var descriptionSpecification in this.MainViewModel.Profile.ExternalDescriptions) {
                if (!(await descriptionSpecification.FetchFromFolderAsync(comicFolder) is { } description)) {
                    continue;
                }

                descriptionAdded = true;

                var text = new Run { Text = description.Content };

                switch (description.DescriptionType) {
                    case ExternalDescriptionType.Text:
                        infoPivotText.Inlines.Add(text);
                        break;
                    case ExternalDescriptionType.Link:
                        var link = new Hyperlink();

                        try {
                            link.NavigateUri = new Uri(description.Content);
                        } catch (UriFormatException) {
                            // do nothing
                        }

                        link.Inlines.Add(text);
                        infoPivotText.Inlines.Add(link);
                        break;
                    default:
                        throw new ProgrammerError("Unhandled switch case");
                }

                infoPivotText.Inlines.Add(new LineBreak());
            }

            return descriptionAdded;
        }

        #endregion
    }
}
