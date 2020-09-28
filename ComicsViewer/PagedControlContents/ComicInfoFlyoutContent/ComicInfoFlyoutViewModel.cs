using ComicsLibrary;
using ComicsViewer.Common;
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
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicInfoFlyoutViewModel : ViewModelBase {
        internal readonly ComicItemGridViewModel ParentViewModel;
        internal readonly ComicWorkItem Item;
        private MainViewModel MainViewModel => this.ParentViewModel.MainViewModel;

        public ComicInfoFlyoutViewModel(ComicItemGridViewModel parentViewModel, ComicWorkItem item) {
            this.ParentViewModel = parentViewModel;
            this.Item = item;
        }


        public async Task InitializeAsync() {
            try {
                foreach (var item in await this.MainViewModel.Profile.GetComicSubitemsAsync(this.Item.Comic)) {
                    this.ComicSubitems.Add(item);
                }
            } catch (UnauthorizedAccessException) {
                await ExpectedExceptions.UnauthorizedAccessAsync();
            } catch (FileNotFoundException) {
                await ExpectedExceptions.ComicNotFoundAsync(this.Item.Comic);
            }

            this.IsLoadingSubItems = false;
            this.OnPropertyChanged(nameof(this.IsLoadingSubItems));
        }

        #region Open Comic - Subitems

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
                comicFolder = await StorageFolder.GetFolderFromPathAsync(this.Item.Comic.Path);
            } catch (FileNotFoundException) {
                return false;
            } catch (UnauthorizedAccessException) {
                return false;
            }

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
                            throw new ProgrammerError("Unhandled switch case");
                    }

                    infoPivotText.Inlines.Add(new LineBreak());
                }
            }

            return descriptionAdded;
        }

        #endregion
    }
}
