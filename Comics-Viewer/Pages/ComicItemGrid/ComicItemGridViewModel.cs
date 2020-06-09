using ComicsViewer.Profiles;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Popups;

#nullable enable

namespace ComicsViewer {
    public class ComicItemGridViewModel : ViewModelBase {
        /* automatically managed properties */
        private int selectedSortIndex;
        public int SelectedSortIndex {
            get => this.selectedSortIndex;
            set {
                if (this.selectedSortIndex == value) {
                    return;
                }

                this.selectedSortIndex = value;
                this.OnPropertyChanged();
            }
        }
        /* semi-manually managed properties */
        public List<ComicItem> ComicItems { get; private set; }
        private void SetComicItems(List<ComicItem> items) {
            if (items == this.ComicItems) {
                return;
            }

            this.ComicItems = items;
            this.OnPropertyChanged(nameof(this.ComicItems));
        }
        /* manually managed properties */
        public string[] SortSelectors => Sorting.SortSelectorNames;
        public int ImageHeight => this.MainViewModel.Profile.ImageHeight;
        public int ImageWidth => this.MainViewModel.Profile.ImageWidth;
        public string ProfileName => this.MainViewModel.Profile.Name;

        internal readonly MainViewModel MainViewModel;

        /* pageType is used to remember the last sort by selection for each type of 
         * page (navigation tabs + details page) or to behave differently when navigating to different types of pages. 
         * It's not pretty but it's a very tiny part of the program. */
        public ComicItemGridViewModel(MainViewModel appViewModel, IEnumerable<ComicItem> comicItems) {
            this.ComicItems = comicItems.ToList();
            this.MainViewModel = appViewModel;

            // Note: please keep this line before setting SelectedSortIndex...
            this.PropertyChanged += this.ComicViewModel_PropertyChanged;
            this.MainViewModel.ProfileChanged += this.AppViewModel_ProfileChanged;

            this.SelectedSortIndex = Defaults.SettingsAccessor.GetLastSortSelection(this.MainViewModel.ActiveNavigationTag);
        }

        // Helper function to open GridView.SelectedItems
        internal Task OpenItems(IList<object> items) {
            return this.OpenItems(items.Cast<ComicItem>());
        }

        public async Task OpenItems(IEnumerable<ComicItem> items) {
            if (items.First() is ComicNavigationItem navigationItem) {
                if (items.Count() != 1) {
                    throw new ApplicationLogicException("Should not allow the user to open multiple navigation" +
                                                        " items at once (use the search into feature instead)");
                }

                this.MainViewModel.NavigateInto(navigationItem);
                return;
            }

            // Only work items remain at this point
            // Although we don't have to await these, we will need to do so for it to throw an 
            // UnauthorizedAccessException when broadFileSystemAccess isn't enabled.
            try {
                var tasks = items.Cast<ComicWorkItem>().Select(item => Startup.OpenComic(item.Comic, this.MainViewModel.Profile));
                await Task.WhenAll(tasks);
            } catch (UnauthorizedAccessException) {
                _ = await new MessageDialog("Please enable file system access in settings to open comics.", "Access denied").ShowAsync();
            } catch (FileNotFoundException) {
                _ = await new MessageDialog("The application is not currently capable of handling this error.", "File not found").ShowAsync();
            }
        }

        /* Instead of putting logic in each observable property's setter, we put them here, to keep setter code the
         * same for each property */
        private void ComicViewModel_PropertyChanged(object _, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(this.SelectedSortIndex):
                    Defaults.SettingsAccessor.SetLastSortSelection(this.MainViewModel.ActiveNavigationTag, this.SelectedSortIndex);
                    this.SetComicItems(Sorting.Sorted(this.ComicItems, (Sorting.SortSelector)this.SelectedSortIndex));
                    break;
            }
        }

        private void AppViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            this.OnPropertyChanged(nameof(this.ImageHeight));
            this.OnPropertyChanged(nameof(this.ImageWidth));
            this.OnPropertyChanged(nameof(this.ProfileName));
        }
    }
}
