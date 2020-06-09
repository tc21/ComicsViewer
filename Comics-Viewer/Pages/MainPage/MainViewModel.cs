using ComicsLibrary;
using ComicsViewer.ComicGrid;
using ComicsViewer.Filters;
using ComicsViewer.Profiles;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Animation;

#nullable enable

namespace ComicsViewer {
    public class MainViewModel {
        private ComicStore comicStore = ComicStore.EmptyComicStore;

        public MainViewModel() {
            this.Filter.FilterChanged += this.Filter_FilterChanged;
        }

        #region Profiles 

        internal UserProfile Profile = new UserProfile { Name = "<uninitialized>" };

        public Task SetDefaultProfile() {
            var suggestedProfile = Defaults.SettingsAccessor.LastProfile;

            if (!ProfileManager.LoadedProfiles.Contains(suggestedProfile)) {
                if (ProfileManager.LoadedProfiles.Count == 0) {
                    throw new ApplicationLogicException("The application in its current state only allows using pre-made profiles.");
                }

                suggestedProfile = ProfileManager.LoadedProfiles[0];
            }

            return this.SetProfileAsync(suggestedProfile);
        }

        public async Task SetProfileAsync(string newProfileName) {
            if (!ProfileManager.LoadedProfiles.Contains(newProfileName)) {
                throw new ApplicationLogicException("The application should not allow the user to switch to a non-existent profile.");
            }

            // update internal modeling
            Defaults.SettingsAccessor.LastProfile = newProfileName;

            this.Profile = await ProfileManager.LoadProfileAsync(newProfileName);
            this.comicStore = await ComicStore.CreateComicsStoreAsync(Profile);
            this.Filter.Clear();

            this.ProfileChanged?.Invoke(this, new ProfileChangedEventArgs { NewProile = this.Profile });
        }

        public event ProfileChangedEventHandler? ProfileChanged;
        public delegate void ProfileChangedEventHandler(MainViewModel sender, ProfileChangedEventArgs e);

        #endregion

        #region Navigation

        internal const string DefaultNavigationTag = "comics";
        internal const string SecondLevelNavigationTag = "default";

        private string selectedTopLevelNavigationTag = "";
        public int NavigationLevel { get; private set; }
        public string ActiveNavigationTag => this.NavigationLevel == 0 ? this.selectedTopLevelNavigationTag : SecondLevelNavigationTag;

        public void Navigate(string navigationTag, NavigationTransitionInfo? transitionInfo = null) {
            var navigationType = (navigationTag == this.ActiveNavigationTag) ? NavigationType.Scroll : NavigationType.New;

            this.selectedTopLevelNavigationTag = navigationTag;
            this.NavigationLevel = 0;

            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs {
                PageType = typeof(ComicItemGridTopLevelContainer),
                Tag = navigationTag,
                TransitionInfo = transitionInfo ?? new EntranceNavigationTransitionInfo(),
                NavigationType = navigationType,
                ComicItems = this.comicStore.ComicItemsForPage(this.Filter, navigationTag)
            });
        }

        public void NavigateOut() {
            this.NavigationLevel = 0;
            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs { NavigationType = NavigationType.Back });
        }

        public void NavigateInto(ComicItem item, NavigationTransitionInfo? transitionInfo = null) {
            this.NavigationLevel = 1;
            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs {
                PageType = typeof(ComicItemGridSecondLevelContainer),
                Tag = SecondLevelNavigationTag,
                NavigationType = NavigationType.New,
                TransitionInfo = transitionInfo ?? new EntranceNavigationTransitionInfo(),
                ComicItems = item.Comics.Select(comic => ComicItem.WorkItem(comic))
            });
        }

        public void NavigateIntoSelected(IEnumerable<ComicItem> items) {
            var comics = items.SelectMany(item => item.Comics).Select(item => item.UniqueIdentifier).ToHashSet();
            this.Filter.GeneratedFilter = comic => comics.Contains(comic.UniqueIdentifier);
        }

        public void RefreshPage() {
            if (this.ActiveNavigationTag == "") {
                // This means the page isn't initialized for some reason, such as before the profile has even been load
                return;
            }

            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs { 
                PageType = (this.NavigationLevel == 0) ? typeof(ComicItemGridTopLevelContainer) : typeof(ComicItemGridSecondLevelContainer),
                Tag = this.ActiveNavigationTag,
                NavigationType = NavigationType.Refresh,
                ComicItems = this.comicStore.ComicItemsForPage(this.Filter, this.ActiveNavigationTag)
            });
        }

        public event NavigationRequestedEventHandler? NavigationRequested;
        public delegate void NavigationRequestedEventHandler(MainViewModel sender, NavigationRequestedEventArgs e);

        #endregion

        #region Search and filtering

        internal readonly Filter Filter = new Filter();

        private void Filter_FilterChanged(Filter filter) {
            this.RefreshPage();
        }
        // Called when a search is successfully compiled and submitted
        // Note: this happens at AppViewModel because filters are per-app
        public void SubmitSearch(Func<Comic, bool> search) {
            Filter.Search = search;
        }

        public FilterPageNavigationArguments GetFilterPageNavigationArguments() {
            var (categories, authors, tags) = this.comicStore.GetAuxiliaryInfo(this.Filter);

            return new FilterPageNavigationArguments {
                Filter = this.Filter,
                VisibleAuthors = authors,
                VisibleCategories = categories,
                VisibleTags = tags
            };
        }

        #endregion
    }

    public class ProfileChangedEventArgs {
        public UserProfile NewProile { get; set; } = new UserProfile();
    }

    public class NavigationRequestedEventArgs {
        public Type? PageType { get; set; }
        public string? Tag { get; set; }
        public IEnumerable<ComicItem>? ComicItems { get; set; }

        public NavigationTransitionInfo TransitionInfo { get; set; } = new EntranceNavigationTransitionInfo();
        public NavigationType NavigationType { get; set; } = NavigationType.New;
    }

    public enum NavigationType {
        Back, New, Scroll, Refresh
    }
}
