using ComicsViewer.Profiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.ViewModels {
    class ComicViewModel : ViewModel {
        /* semi-manually managed properties */
        public List<ComicItem> ComicItems { get; private set; }
        private void SetComicItems(List<ComicItem> items) {
            if (items == this.ComicItems) {
                return;
            }

            this.ComicItems = items;
            this.OnPropertyChanged(nameof(this.ComicItems));
        }
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

        internal readonly string PageType;

        /* pageType is used to remember the last sort by selection for each type of 
         * page (navigation tabs + details page) or to behave differently when navigating to different types of pages. 
         * It's not pretty but it's a very tiny part of the program. */
        internal ComicViewModel(UserProfile profile, IEnumerable<ComicItem> comicItems, string pageType) : base(profile) {

            this.ComicItems = comicItems.ToList();
            this.PageType = pageType;

            // Note: please keep this line before setting SelectedSortIndex...
            this.PropertyChanged += this.ComicViewModel_PropertyChanged;

            this.SelectedSortIndex = Defaults.SettingsAccessor.GetLastSortSelection(this.PageType);
        }

        /* Instead of putting logic in each observable property's setter, we put them here, to keep setter code the
         * same for each property */
        private void ComicViewModel_PropertyChanged(object _, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(this.SelectedSortIndex):
                    Defaults.SettingsAccessor.SetLastSortSelection(this.PageType, this.SelectedSortIndex);
                    this.SetComicItems(Sorting.Sorted(this.ComicItems, (Sorting.SortSelector)this.SelectedSortIndex));
                    break;
            }
        }
    }
}
