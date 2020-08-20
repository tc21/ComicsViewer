using ComicsLibrary;
using ComicsLibrary.SQL;
using ComicsViewer.Features;
using ComicsViewer.Pages;
using ComicsViewer.Support;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TC.Database.MicrosoftSqlite;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class MainViewModel : ViewModelBase {
        internal readonly MainComicList Comics = new MainComicList();
        public ComicView ComicView => Comics.Filtered();

        public MainViewModel() {
            this.ProfileChanged += this.MainViewModel_ProfileChanged;
        }

        #region Profiles 

        internal UserProfile Profile = new UserProfile { Name = "<uninitialized>" };
        public bool IsLoadingProfile { get; private set; } = false;

        public async Task SetDefaultProfileAsync() {
            var suggestedProfile = Defaults.SettingsAccessor.LastProfile;

            if (ProfileManager.LoadedProfiles.Count == 0) {
                var profile = await ProfileManager.CreateProfileAsync();
                await this.SetProfileAsync(profile.Name);
                return;
            }

            if (!ProfileManager.LoadedProfiles.Contains(suggestedProfile)) {
                suggestedProfile = ProfileManager.LoadedProfiles[0];
            }

            await this.SetProfileAsync(suggestedProfile);
        }

        public async Task SetProfileAsync(string newProfileName) {
            if (!ProfileManager.LoadedProfiles.Contains(newProfileName)) {
                throw new ApplicationLogicException("The application should not allow the user to switch to a non-existent profile.");
            }

            this.IsLoadingProfile = true;
            this.OnPropertyChanged(nameof(this.IsLoadingProfile));

            // Cancel tasks before switching
            if (this.IsTaskRunning) {
                foreach (var item in this.taskNames.Keys.ToList()) {
                    _ = this.RequestCancelTask(item);
                }

                while (this.IsTaskRunning) {
                    await Task.Delay(50);
                }
            }

            // update internal modeling
            Defaults.SettingsAccessor.LastProfile = newProfileName;

            this.Profile = await ProfileManager.LoadProfileAsync(newProfileName);
            this.ProfileChanged?.Invoke(this, new ProfileChangedEventArgs { NewProile = this.Profile });

            this.IsLoadingProfile = false;
            this.OnPropertyChanged(nameof(this.IsLoadingProfile));
        }

        private async void MainViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            if (e.NewProile != this.Profile) {
                throw new ApplicationLogicException();
            }

            var manager = await this.GetComicsManagerAsync(migrate: true);
            this.Comics.Refresh(await manager.GetAllComicsAsync());

            this.Comics.Filter.Clear();

            await this.RequestValidateAndRemoveComicsAsync();
        }

        /* We aren't disposing of the connection on our own, since I havent figured out how to without writing a new class */
        private async Task<ComicsManager> GetComicsManagerAsync(bool migrate = false) {
            var connection = new SqliteConnection($"Filename={this.Profile.DatabaseFileName}");
            await connection.OpenAsync();
            var dbconnection = new SqliteDatabaseConnection(connection);

            if (migrate) {
                return ComicsManager.MigratedComicsManager(dbconnection);
            }

            return new ComicsManager(dbconnection);
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
        public int NavigationLevel { get; set; }
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
                NavigationType = navigationType
            });
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
            this.NavigateIntoComics(items.SelectMany(item => item.Comics));
        }

        public void NavigateIntoAuthor(string displayName) {
            this.NavigateIntoComics(this.Comics.Where(comic => comic.DisplayAuthor == displayName));
        }

        private void NavigateIntoComics(IEnumerable<Comic> comics) {
            var identifiers = comics.Select(item => item.UniqueIdentifier).ToHashSet();
            this.Comics.Filter.GeneratedFilter = comic => identifiers.Contains(comic.UniqueIdentifier);
            this.Comics.Filter.Metadata.GeneratedFilterItemCount = identifiers.Count;
        }

        public event NavigationRequestedEventHandler? NavigationRequested;
        public delegate void NavigationRequestedEventHandler(MainViewModel sender, NavigationRequestedEventArgs e);

        #endregion

        #region Search and filtering

        //internal readonly Filter Filter = new Filter();

        // Called when a search is successfully compiled and submitted
        // Note: this happens at AppViewModel because filters are per-app
        public void SubmitSearch(Func<Comic, bool> search, string? searchText = null) {
            this.Comics.Filter.Search = search;

            if (searchText != null) {
                this.Comics.Filter.Metadata.SearchPhrase = searchText;

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

        public FilterFlyoutNavigationArguments GetFilterPageNavigationArguments(ComicItemGridViewModel parentViewModel) {
            var info = this.GetAuxiliaryInfo(this.Comics.Filter);

            return new FilterFlyoutNavigationArguments {
                Filter = this.Comics.Filter,
                ParentViewModel = parentViewModel,
                AuxiliaryInfo = info
            };
        }

        private FilterViewAuxiliaryInfo GetAuxiliaryInfo(Filter? filter) {
            var categories = new DefaultDictionary<string, int>();
            var authors = new DefaultDictionary<string, int>();
            var tags = new DefaultDictionary<string, int>();

            foreach (var comic in this.Comics) {
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
        public readonly ObservableCollection<ComicTask> Tasks = new ObservableCollection<ComicTask>();
        public bool IsTaskRunning => this.Tasks.Count > 0;
        private readonly Dictionary<string, ComicTask> taskNames = new Dictionary<string, ComicTask>();

        private bool ScheduleTask<T>(
                string tag, string description, ComicTask.ComicTaskDelegate<T> asyncAction, 
                Func<T, Task>? asyncCallback, Func<Exception, Task<bool>>? exceptionHandler) {
            if (taskNames.ContainsKey(tag)) { 
                return false;
            }

            var comicTask = new ComicTask(description, async (cc, p) => (await asyncAction(cc, p))!);
            comicTask.TaskCompleted += async (task, result)  => {
                _ = this.taskNames.Remove(tag);
                _ = this.Tasks.Remove(task);

                if (task.IsFaulted) {
                    if (task.StoredException == null) {
                        throw new ApplicationLogicException("A faulted task should have its StoredException property set!");
                    }

                    if (exceptionHandler == null) {
                        // By default, let's notify the user of IntendedBehaviorExceptions (instead of crashing)
                        exceptionHandler = ExpectedExceptions.HandleIntendedBehaviorExceptionsAsync;
                    }

                    if (await exceptionHandler(task.StoredException) == false) {
                        _ = await new MessageDialog(
                            task.StoredException.ToString(),
                            $"Unhandled {task.StoredException!.GetType()} when running task {task.Name}"
                        ).ShowAsync();
                    }
                }

                if (task.IsCompleted && asyncCallback != null) {
                    await asyncCallback((T)result!);
                }

                this.OnPropertyChanged(nameof(this.IsTaskRunning));
            };

            this.taskNames.Add(tag, comicTask);
            this.Tasks.Add(comicTask);
            comicTask.Start();
            this.OnPropertyChanged(nameof(this.IsTaskRunning));

            return true;
        }

        public bool RequestCancelTask(string tag) {
            if (!this.taskNames.ContainsKey(tag)) {
                return false;
            }

            var task = this.taskNames[tag];
            _ = task.Cancel();
            return true;
        }

        public async Task<bool> CancelTaskAsync(string tag) {
            if (this.RequestCancelTask(tag) == false) {
                return false;
            }

            while (this.taskNames.ContainsKey(tag)) {
                await Task.Delay(100);
            }

            return true;
        }

        public Task StartUniqueTaskAsync(
                string tag, string name, ComicTask.ComicTaskDelegate asyncAction, 
                Func<Task>? asyncCallback = null, Func<Exception, Task<bool>>? exceptionHandler = null) {
            return this.StartUniqueTaskAsync(tag, name, 
                asyncAction: async (cc, p) => { await asyncAction(cc, p); return 0; },
                asyncCallback: asyncCallback == null ? null : (Func<int, Task>)(_ => asyncCallback()),
                exceptionHandler: exceptionHandler);
        }

        public async Task StartUniqueTaskAsync<T>(
                string tag, string name, ComicTask.ComicTaskDelegate<T> asyncAction, 
                Func<T, Task>? asyncCallback = null, Func<Exception, Task<bool>>? exceptionHandler = null) {
            if (!this.ScheduleTask(tag, name, asyncAction, asyncCallback, exceptionHandler)) {
                _ = await new MessageDialog(
                   $"A task with tag '{tag}' is already running. Please wait for it to finish.",
                    "Cannot start task"
                ).ShowAsync();
            }
        }

        public async Task RequestReloadAllComicsAsync() {
            await this.StartUniqueTaskAsync("reload", "Reloading all comics...", 
                (cc, p) => ComicsLoader.FromProfilePathsAsync(this.Profile, cc, p),
                async result => {
                    // TODO: we should probably figure out how to only update what we need
                    var manager = await this.GetComicsManagerAsync();
                    await manager.AssignKnownMetadataAsync(result);

                    // A reload all completely replaces the current comics list. We won't send any events. We'll just refresh everything.
                    await this.RemoveComicsAsync(this.Comics);
                    _ = await this.AddComicsWithoutReplacingAsync(result);
                }, 
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public async Task RequestReloadCategoryAsync(NamedPath category) {
            await this.StartUniqueTaskAsync("reload", $"Reloading category '{category.Name}'...",
                (cc, p) => ComicsLoader.FromRootPathAsync(this.Profile, category, cc, p),
                async result => {
                    var manager = await this.GetComicsManagerAsync();
                    await manager.AssignKnownMetadataAsync(result);

                    await this.RemoveComicsAsync(this.Comics.Where(comic => comic.Category == category.Name));

                    var notAdded = await this.AddComicsWithoutReplacingAsync(result);
                    if (notAdded.Count > 0) {
                        await this.NotifyComicsNotAdded(notAdded);
                    }
                },
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public async Task RequestLoadComicsFromFoldersAsync(IEnumerable<StorageFolder> folders) {
            await this.StartUniqueTaskAsync("reload", $"Adding comics from {folders.Count()} folders...",
                (cc, p) => ComicsLoader.FromImportedFoldersAsync(this.Profile, folders, cc, p),
                async result => {
                    var manager = await this.GetComicsManagerAsync();
                    await manager.AssignKnownMetadataAsync(result);

                    var notAdded = await this.AddComicsWithoutReplacingAsync(result);
                    if (notAdded.Count > 0) {
                        await this.NotifyComicsNotAdded(notAdded);
                    }
                },
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        private async Task RequestValidateAndRemoveComicsAsync() {
            await this.StartUniqueTaskAsync("validate", $"Validating {this.Comics.Count()} comics...",
                (cc, p) => ComicsLoader.FindInvalidComics(this.Comics, this.Profile, checkFiles: false, cc, p),
                async result => await this.RemoveComicsAsync(result),
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        /// <summary>
        /// returns the list of comics that were not added, because they already exist. 
        /// A successsful add where all comics were added would return an empty list.
        /// </summary>
        private async Task<List<Comic>> AddComicsWithoutReplacingAsync(IEnumerable<Comic> comics) {
            var added = new List<Comic>();
            var duplicates = new List<Comic>();

            // we have to make a copy of comics, since the user might just pass this.comics (or more likely a query
            // based on it) to this function
            foreach (var comic in comics.ToList()) {
                if (!this.Comics.Contains(comic)) {
                    added.Add(comic);
                } else {
                    duplicates.Add(comic);
                }
            }

            this.Comics.Add(added);

            var manager = await this.GetComicsManagerAsync();
            await manager.AddOrUpdateComicsAsync(added);

            return duplicates;
        }

        private async Task NotifyComicsNotAdded(IEnumerable<Comic> comics) {
            var message = "The following items were not added because they already exist:\n";

            foreach (var comic in comics) {
                message += $"\n{comic.UniqueIdentifier}";
            }

            _ = await new MessageDialog(message, "Warning: items not added").ShowAsync();
        }

        public async Task RemoveComicsAsync(IEnumerable<Comic> comics) {
            var removed = new List<Comic>();

            foreach (var comic in comics.ToList()) {
                if (this.Comics.Contains(comic)) {
                    removed.Add(comic);
                }
            }

            this.Comics.Remove(removed);

            var manager = await this.GetComicsManagerAsync();
            await manager.RemoveComicsAsync(removed);
        }

        /* The program only knows how to change one comic at a time, so we'll generalize this function when we get there */
        public async Task NotifyComicsChangedAsync(IEnumerable<Comic> comics, bool thumbnailChanged = false) {
            this.Comics.NotifyModification(comics);

            var manager = await this.GetComicsManagerAsync();
            await manager.AddOrUpdateComicsAsync(comics);
        }

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
}
