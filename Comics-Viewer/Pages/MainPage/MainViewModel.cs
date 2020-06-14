using ComicsLibrary;
using ComicsLibrary.SQL;
using ComicsViewer.ComicGrid;
using ComicsViewer.Filters;
using ComicsViewer.Pages.Helpers;
using ComicsViewer.Profiles;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TC.Database.MicrosoftSqlite;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media.Animation;

#nullable enable

namespace ComicsViewer {
    public class MainViewModel : ViewModelBase {
        private List<Comic> comics = new List<Comic>();

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
            
            
            using var connection = new SqliteConnection($"Filename={this.Profile.DatabaseFileName}");
            await connection.OpenAsync();
            var manager = ComicsManager.MigratedComicsManager(new SqliteDatabaseConnection(connection));
            this.comics = (await manager.GetAllComicsAsync()).ToList();

            this.Filter.Clear();

            this.ProfileChanged?.Invoke(this, new ProfileChangedEventArgs { NewProile = this.Profile });
        }

        public event ProfileChangedEventHandler? ProfileChanged;
        public delegate void ProfileChangedEventHandler(MainViewModel sender, ProfileChangedEventArgs e);

        internal void NotifyProfileChanged(ProfileChangeType type) {
            this.ProfileChanged?.Invoke(this, new ProfileChangedEventArgs { ChangeType = type, NewProile = this.Profile });
        }

        #endregion

        #region Navigation

        internal const string DefaultNavigationTag = "comics";
        internal const string SecondLevelNavigationTag = "default";

        private string selectedTopLevelNavigationTag = "";
        public int NavigationLevel { get; private set; }
        public string ActiveNavigationTag => this.NavigationLevel == 0 ? this.selectedTopLevelNavigationTag : SecondLevelNavigationTag;

        public void Navigate(string navigationTag, NavigationTransitionInfo? transitionInfo = null, bool ignoreCache = false) {
            var navigationType = (ignoreCache || navigationTag != this.ActiveNavigationTag) 
                ? NavigationType.New : NavigationType.Scroll;

            this.selectedTopLevelNavigationTag = navigationTag;
            this.NavigationLevel = 0;

            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs {
                PageType = typeof(ComicItemGridTopLevelContainer),
                Tag = navigationTag,
                TransitionInfo = transitionInfo ?? new EntranceNavigationTransitionInfo(),
                NavigationType = navigationType,
                Comics = this.comics
            });
            ;
        }

        public void NavigateOut(bool ignoreCache = false) {
            if (ignoreCache) {
                this.Navigate(this.selectedTopLevelNavigationTag, ignoreCache: true);
                return;
            }

            this.NavigationLevel = 0;
            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs { 
                NavigationType = NavigationType.Back,
                Tag = this.selectedTopLevelNavigationTag
            });
        }

        public void NavigateInto(ComicItem item, NavigationTransitionInfo? transitionInfo = null) {
            this.NavigationLevel = 1;
            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs {
                PageType = typeof(ComicItemGridSecondLevelContainer),
                Tag = SecondLevelNavigationTag,
                NavigationType = NavigationType.New,
                TransitionInfo = transitionInfo ?? new EntranceNavigationTransitionInfo(),
                Comics = item.Comics
            });
        }

        public void NavigateIntoSelected(IEnumerable<ComicItem> items) {
            var comics = items.SelectMany(item => item.Comics).Select(item => item.UniqueIdentifier).ToHashSet();
            this.Filter.GeneratedFilter = comic => comics.Contains(comic.UniqueIdentifier);
            this.Filter.Metadata.GeneratedFilterItemCount = comics.Count;
        }

        public event NavigationRequestedEventHandler? NavigationRequested;
        public delegate void NavigationRequestedEventHandler(MainViewModel sender, NavigationRequestedEventArgs e);

        #endregion

        #region Search and filtering

        internal readonly Filter Filter = new Filter();

        // Called when a search is successfully compiled and submitted
        // Note: this happens at AppViewModel because filters are per-app
        public void SubmitSearch(Func<Comic, bool> search, string? searchText = null) {
            this.Filter.Search = search;

            if (searchText != null) {
                this.Filter.Metadata.SearchPhrase = searchText;

                // Add this search to the recents list
                if (searchText.Trim() != "") {
                    var savedSearches = Defaults.SettingsAccessor.SavedSearches;
                    RemoveIgnoreCase(ref savedSearches, searchText);
                    savedSearches.Insert(0, searchText);

                    while (savedSearches.Count > 4) {
                        savedSearches.RemoveAt(4);
                    }

                    Defaults.SettingsAccessor.SavedSearches = savedSearches;
                }
            }

            // Helper function
            static void RemoveIgnoreCase(ref IList<string> list, string text) {
                var removes = new List<int>();

                for (var i = 0; i < list.Count; i++) {
                    if (list[i].Equals(text, StringComparison.OrdinalIgnoreCase)) {
                        removes.Insert(0, i);
                    }
                }

                foreach (var i in removes) {
                    list.RemoveAt(i);
                }
            }
        }

        public FilterPageNavigationArguments GetFilterPageNavigationArguments(ComicItemGridViewModel parentViewModel) {
            var info = this.GetAuxiliaryInfo(this.Filter);

            return new FilterPageNavigationArguments {
                Filter = this.Filter,
                ParentViewModel = parentViewModel,
                AuxiliaryInfo = info
            };
        }

        private FilterViewAuxiliaryInfo GetAuxiliaryInfo(Filter? filter) {
            var categories = new DefaultDictionary<string, int>();
            var authors = new DefaultDictionary<string, int>();
            var tags = new DefaultDictionary<string, int>();

            foreach (var comic in this.comics) {
                if (filter != null && !filter.ShouldBeVisible(comic)) {
                    continue;
                }

                categories[comic.DisplayCategory] += 1;
                authors[comic.DisplayAuthor] += 1;
                foreach (var tag in comic.Tags) {
                    tags[tag] += 1;
                }
            }

            return new FilterViewAuxiliaryInfo(categories, authors, tags);
        }
        #endregion

        #region Adding, removing, reloading comics

        /* The purpose of this section is to allow for ComicItemGrids to be notified of changes to the master list of
         * comics, so we don't have to reload the entire list of comics each time something is changed. */
        public bool IsTaskRunning => this.TaskName != null;
        public string? TaskName { get; private set; }
        public int TaskProgress { get; private set; } = 0;
        private CancellationTokenSource? taskCancellationTokenSource;

        /* Starts a cpu-heavy task that can be cancelled. We only allow one such task per application.
         * Returnus null if the task was cancelled. */
        private async Task<T?> StartTaskAsync<T>(string name, Func<CancellationToken, IProgress<int>, Task<T>> task) where T : class {
            if (this.IsTaskRunning) {
                _ = await new MessageDialog("A task is already running. Please wait for it to finish.", "Cannot start task").ShowAsync();
                return null;
            }

            this.TaskName = name;
            this.taskCancellationTokenSource = new CancellationTokenSource();
            var taskProgress = new Progress<int>(progress => {
                this.TaskProgress = progress;
                this.OnPropertyChanged(nameof(this.TaskProgress));
            });

            this.OnPropertyChanged(nameof(this.IsTaskRunning));
            this.OnPropertyChanged(nameof(this.TaskName));

            var result = await Task.Run(() => task(this.taskCancellationTokenSource.Token, taskProgress), this.taskCancellationTokenSource.Token);

            this.TaskName = null;
            this.OnPropertyChanged(nameof(this.IsTaskRunning));

            if (this.taskCancellationTokenSource.IsCancellationRequested) {
                return null;
            }

            return result;
        }

        public void RequestClearActiveTask() {
            this.taskCancellationTokenSource!.Cancel();
        }

        public async Task RequestReloadAllComicsAsync() {
            // Design philosophy: we could have this method return bool, and the caller show message boxes, but that
            // doesn't actually make the code more elegant in any way
            var comics = await this.StartTaskAsync("Reloading all comics...", 
                (cc, p) => ComicsLoader.FromProfilePathsAsync(this.Profile, cc, p));

            if (comics == null) {
                return;
            }

            // TODO we should diff with this.comics, do whatever we need to, update the database, etc...
            this.comics = comics.ToList();
            this.Navigate(this.selectedTopLevelNavigationTag, ignoreCache: true);
        }

        public async Task RequestReloadCategoryAsync(NamedPath category) {
            var comics = await this.StartTaskAsync($"Reloading category '{category.Name}'...", 
                (cc, p) => ComicsLoader.FromRootPathAsync(this.Profile, category, cc, p));

            if (comics == null) {
                return;
            }

            // TODO diff, etc.
            this.comics = comics.ToList();
            this.Navigate(this.selectedTopLevelNavigationTag, ignoreCache: true);
        }


        public async Task RequestLoadComicsFromFoldersAsync(IEnumerable<StorageFolder> folders) {
            var comics = await this.StartTaskAsync($"Adding comics from {folders.Count()} folders...",
                (cc, p) => ComicsLoader.FromImportedFoldersAsync(this.Profile, folders, cc, p));

            if (comics == null) {
                return;
            }

            // TODO check for duplicates
            this.comics.AddRange(comics);
            this.Navigate(this.selectedTopLevelNavigationTag, ignoreCache: true);
        }

        /* Example functions subject to change */
        public void AddComics(IEnumerable<Comic> comics) {
            var copy = comics.ToList();
            // TODO validation?
            this.comics.AddRange(copy);
            this.ComicsModified(this, new ComicsModifiedEventArgs(ComicModificationType.ItemsAdded, copy));
        }

        public void RemoveComics(IEnumerable<Comic> comics) {
            var copy = comics.ToList();
            // TODO validation
            foreach (var item in copy) {
                this.comics.Remove(item);
            }

            this.ComicsModified(this, new ComicsModifiedEventArgs(ComicModificationType.ItemsRemoved, copy));
        }

        public event ComicsModifiedEventHandler ComicsModified = delegate { };
        public delegate void ComicsModifiedEventHandler(MainViewModel sender, ComicsModifiedEventArgs e);

        #endregion
    }

    public class ProfileChangedEventArgs {
        public ProfileChangeType ChangeType = ProfileChangeType.ProfileChanged;
        public UserProfile NewProile { get; set; } = new UserProfile();
    }

    public enum ProfileChangeType {
        ProfileChanged, SettingsChanged
    }

    public class NavigationRequestedEventArgs {
        public Type? PageType { get; set; }
        public string? Tag { get; set; }
        public IEnumerable<Comic>? Comics { get; set; }

        public NavigationTransitionInfo TransitionInfo { get; set; } = new EntranceNavigationTransitionInfo();
        public NavigationType NavigationType { get; set; } = NavigationType.New;
    }

    public enum NavigationType {
        Back, New, Scroll
    }

    public class ComicsModifiedEventArgs {
        public ComicModificationType ModificationType { get; }
        public IEnumerable<Comic> ModifiedComics { get; }

        public ComicsModifiedEventArgs(ComicModificationType modificationType, IEnumerable<Comic> modifiedComics) {
            this.ModificationType = modificationType;
            this.ModifiedComics = modifiedComics;
        }
    }

    public enum ComicModificationType {
        ItemsAdded, ItemsRemoved
        // ItemsModified: handled by ComicItem, so we don't need to do anything here.
        // Refresh: handled by MainViewModel: there's nothing subviews can do anyway
        // Note: if we implement the feature to combine multiple works into one, we will need more complicated behavior
    }
}
