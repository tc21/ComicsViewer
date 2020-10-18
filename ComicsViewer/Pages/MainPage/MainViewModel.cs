using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsLibrary.SQL;
using ComicsLibrary.SQL.Sqlite;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Pages;
using ComicsViewer.Support;
using ComicsViewer.Uwp.Common.Win32Interop;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class MainViewModel : ViewModelBase {
        internal readonly MainComicList Comics = new MainComicList();
        public ComicView ComicView => this.Comics.Filtered();
        public List<Playlist> Playlists { get; } = new List<Playlist>();

        public MainViewModel() {
            this.ProfileChanged += this.MainViewModel_ProfileChanged;
        }

        public string Title { get; private set; } = "Comics";

        private string? PageName {
            set {
                this.Title = value == null
                    ? "Comics"
                    : $"Comics - {value}";

                this.OnPropertyChanged(nameof(this.Title));
            }
        }

        #region Profiles 

        internal UserProfile Profile = new UserProfile { Name = "<uninitialized>" };
        public bool IsLoadingProfile { get; private set; }

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
                throw new ProgrammerError("The application should not allow the user to switch to a non-existent profile.");
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
            this.ProfileChanged?.Invoke(this, new ProfileChangedEventArgs { NewProfile = this.Profile });

            this.IsLoadingProfile = false;
            this.OnPropertyChanged(nameof(this.IsLoadingProfile));
        }

        private async void MainViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            if (e.NewProfile != this.Profile) {
                throw new ProgrammerError();
            }

            var manager = await this.GetComicsManagerAsync(migrate: true);
            this.Comics.Refresh(await manager.GetAllComicsAsync());

            this.Playlists.Clear();
            this.Playlists.AddRange(await manager.GetPlaylistsAsync(this.Comics));

            this.Comics.Filter.Clear();


            // Verify that we have access to the file system
            if (await this.VerifyPermissionForPathsAsync(e.NewProfile.RootPaths.Select(p => p.Path))) {
                await this.StartValidateAndRemoveComicsTaskAsync();
            }
        }

        private async Task<bool> VerifyPermissionForPathsAsync(IEnumerable<string> paths) {
            try {
                foreach (var path in paths) {
                    _ = await StorageFolder.GetFolderFromPathAsync(path);
                }
            } catch (FileNotFoundException) {
                // we allow that
            } catch (UnauthorizedAccessException) {
                await ExpectedExceptions.UnauthorizedAccessAsync(cancelled: false);
                return false;
            }

            return true;
        }

        /* We aren't disposing of the connection on our own, since I havent figured out how to without writing a new class */
        private async Task<ComicsManager> GetComicsManagerAsync(bool migrate = false) {
            var connection = new SqliteConnection($"Filename={this.Profile.DatabaseFileName}");
            await connection.OpenAsync();
            var dbconnection = new SqliteDatabaseConnection(connection);

            if (migrate) {
                return await ComicsManager.MigratedComicsManagerAsync(dbconnection);
            }

            return new ComicsManager(dbconnection);
        }

        public event ProfileChangedEventHandler? ProfileChanged;
        public delegate void ProfileChangedEventHandler(MainViewModel sender, ProfileChangedEventArgs e);

        internal void NotifyProfileChanged(ProfileChangeType type) {
            this.ProfileChanged?.Invoke(this, new ProfileChangedEventArgs { ChangeType = type, NewProfile = this.Profile });
        }

        #endregion

        #region Navigation

        private NavigationTag selectedTopLevelNavigationTag;
        public int NavigationLevel { get; set; }
        public NavigationTag ActiveNavigationTag => this.NavigationLevel == 0 ? this.selectedTopLevelNavigationTag : NavigationTag.Detail;

        public void Navigate(NavigationTag navigationTag, NavigationTransitionInfo? transitionInfo = null, bool ignoreCache = false) {
            var navigationType = ignoreCache || navigationTag != this.ActiveNavigationTag 
                ? NavigationType.New 
                : NavigationType.Scroll;

            this.selectedTopLevelNavigationTag = navigationTag;
            this.NavigationLevel = 0;

            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs {
                PageType = typeof(ComicItemGridTopLevelContainer),
                Tag = navigationTag,
                TransitionInfo = transitionInfo ?? new EntranceNavigationTransitionInfo(),
                NavigationType = navigationType
            });

            this.PageName = null;
        }

        public void NavigateOut(bool ignoreCache = false, ComicItemGrid? of = null) {
            of?.PrepareNavigateOut();

            if (ignoreCache) {
                this.Navigate(this.selectedTopLevelNavigationTag, ignoreCache: true);
                return;
            }

            this.NavigationLevel = 0;
            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs { 
                NavigationType = NavigationType.Back,
                Tag = this.selectedTopLevelNavigationTag
            });

            this.PageName = null;
        }

        // note: we have already validateed that item is a nav item
        public void NavigateInto(ComicNavigationItem item, NavigationTransitionInfo? transitionInfo = null) {
            this.NavigationLevel = 1;
            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs {
                PageType = typeof(ComicItemGridSecondLevelContainer),
                Tag = NavigationTag.Detail,
                NavigationType = NavigationType.New,
                TransitionInfo = transitionInfo ?? new EntranceNavigationTransitionInfo(),
                Comics = item.Comics
            });

            this.PageName = item.Title;
        }

        public void FilterToSelected(IEnumerable<ComicItem> items) {
            this.FilterToComics(items.SelectMany(item => item.ContainedComics()));
        }

        public void FilterToAuthor(string author) {
            var properties = this.Comics.SortedProperties(c => new[] { c.Author }, ComicsLibrary.Sorting.ComicPropertySortSelector.Random);
            var authorView = properties.PropertyView(author);
            var placeholder = new ComicNavigationItem(author, authorView);

            this.NavigateInto(placeholder);
        }

        private void FilterToComics(IEnumerable<Comic> comics) {
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
                // Add this search to the recents list
                if (searchText.Trim() != "") {
                    IList<string> savedSearches = Defaults.SettingsAccessor.GetSavedSearches(this.Profile.Name);
                    RemoveIgnoreCase(ref savedSearches, searchText);
                    savedSearches.Insert(0, searchText);
                    Defaults.SettingsAccessor.SetSavedSearches(this.Profile.Name, savedSearches);
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
            var info = this.GetAuxiliaryInfo();

            return new FilterFlyoutNavigationArguments {
                Filter = this.Comics.Filter,
                ParentViewModel = parentViewModel,
                AuxiliaryInfo = info
            };
        }

        private FilterViewAuxiliaryInfo GetAuxiliaryInfo() {
            var categories = new DefaultDictionary<string, int>();
            var authors = new DefaultDictionary<string, int>();
            var tags = new DefaultDictionary<string, int>();

            foreach (var comic in this.ComicView) {
                categories[comic.Category] += 1;
                authors[comic.Author] += 1;
                foreach (var tag in comic.Tags) {
                    tags[tag] += 1;
                }
            }

            return new FilterViewAuxiliaryInfo(categories, authors, tags);
        }
        #endregion

        #region Tasks

        /* The purpose of this section is to allow for ComicItemGrids to be notified of changes to the master list of
         * comics, so we don't have to reload the entire list of comics each time something is changed. */
        public readonly ObservableCollection<ComicTask> Tasks = new ObservableCollection<ComicTask>();
        public bool IsTaskRunning => this.Tasks.Count > 0;
        private readonly Dictionary<string, ComicTask> taskNames = new Dictionary<string, ComicTask>();

        private bool ScheduleTask<T>(
                string tag, string description, ComicTask.ComicTaskDelegate<T> asyncAction, 
                Func<T, Task>? asyncCallback, Func<Exception, Task<bool>>? exceptionHandler) {
            if (this.taskNames.ContainsKey(tag)) { 
                return false;
            }

            var comicTask = new ComicTask(description, async (cc, p) => (await asyncAction(cc, p))!);
            comicTask.TaskCompleted += async (task, result)  => {
                _ = this.taskNames.Remove(tag);
                _ = this.Tasks.Remove(task);

                if (task.IsFaulted) {
                    if (task.StoredException == null) {
                        throw new ProgrammerError("A faulted task should have its StoredException property set!");
                    }

                    // By default, let's notify the user of IntendedBehaviorExceptions (instead of crashing)
                    exceptionHandler ??= ExpectedExceptions.HandleIntendedBehaviorExceptionsAsync;

                    if (await exceptionHandler(task.StoredException) == false) {
                        _ = await new ContentDialog{
                            Content = task.StoredException.ToString(),
                            Title = $"Unhandled {task.StoredException!.GetType()} when running task {task.Name}",
                            CloseButtonText = "OK"
                        }.ShowAsync();
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

        private bool RequestCancelTask(string tag) {
            if (!this.taskNames.ContainsKey(tag)) {
                return false;
            }

            var task = this.taskNames[tag];
            _ = task.Cancel();
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

        private async Task StartUniqueTaskAsync<T>(
                string tag, string name, ComicTask.ComicTaskDelegate<T> asyncAction, 
                Func<T, Task>? asyncCallback = null, Func<Exception, Task<bool>>? exceptionHandler = null) {
            if (!this.ScheduleTask(tag, name, asyncAction, asyncCallback, exceptionHandler)) {
                _ = await new ContentDialog {
                    Content = $"A task with tag '{tag}' is already running. Please wait for it to finish.",
                    Title = "Cannot start task",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        public async Task StartReloadAllComicsTaskAsync() {
            await this.StartUniqueTaskAsync("reload", "Reloading all comics...", 
                (cc, p) => ComicsLoader.FromProfilePathsAsync(this.Profile, cc, p),
                async result => {
                    // TODO: we should probably figure out how to only update what we need
                    var manager = await this.GetComicsManagerAsync();
                    result = await manager.RetrieveKnownMetadataAsync(result);

                    // A reload all completely replaces the current comics list. We won't send any events. We'll just refresh everything.
                    await this.RemoveComicsAsync(this.Comics);
                    _ = await this.AddComicsWithoutReplacingAsync(result, notifyDuplicates: true);
                }, 
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public async Task StartReloadCategoryTaskAsync(NamedPath category) {
            await this.StartUniqueTaskAsync("reload", $"Reloading category '{category.Name}'...",
                (cc, p) => ComicsLoader.FromRootPathAsync(this.Profile, category, cc, p),
                async result => {
                    var manager = await this.GetComicsManagerAsync();
                    result = await manager.RetrieveKnownMetadataAsync(result);

                    await this.RemoveComicsAsync(this.Comics.Where(comic => comic.Category == category.Name));
                    _ = await this.AddComicsWithoutReplacingAsync(result, notifyDuplicates: true);
                },
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public async Task StartLoadComicsFromFoldersTaskAsync(IEnumerable<StorageFolder> folders) {
            var comicFolders = folders.ToList();

            await this.StartUniqueTaskAsync("reload", $"Adding comics from {comicFolders.Count} folders...",
                (cc, p) => ComicsLoader.FromImportedFoldersAsync(this.Profile, comicFolders, cc, p),
                async result => {
                    var manager = await this.GetComicsManagerAsync();
                    result = await manager.RetrieveKnownMetadataAsync(result);
                    _ = await this.AddComicsWithoutReplacingAsync(result, notifyDuplicates: true);
                },
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        private async Task StartValidateAndRemoveComicsTaskAsync() {
            await this.StartUniqueTaskAsync("validate", $"Validating {this.Comics.Count()} comics...",
                (cc, p) => ComicsLoader.FindInvalidComicsAsync(this.Comics, cc, p),
                async result => {
                    if (result.Any()) {
                        if (await PromptRemoveComicsAsync(result)) {
                            await this.RemoveComicsAsync(result);
                        }
                    }
                },
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public Task StartRenameAuthorTaskAsync(string oldAuthor, string newAuthor) {
            var comics = this.Comics.Where(c => c.Author == oldAuthor).ToList();

            // Note: if we make another function where we need to move files around, we should move this to a new class.
            return this.StartUniqueTaskAsync("moveFiles", $"Moving {comics.Count.PluralString("item")} belonging to author {oldAuthor}...",
                async (cc, p) => {
                    // Step 1. Validation
                    if (newAuthor.Any(c => Path.GetInvalidFileNameChars().Contains(c))) {
                        throw new IntendedBehaviorException("new author name contains invalid filename characters");
                    }

                    EnsureEmpty(
                        comics.Where(c => !IO.FileOrDirectoryExists(c.Path)),
                        "The following items could not be found"
                    );

                    EnsureEmpty(
                        comics.Where(c => !this.Profile.RootPaths.ContainsName(c.Category)),
                        "Could not determine the category for the following items"
                    );

                    var existingComicsUnderNewName = this.Comics.Where(c => c.Author == newAuthor).ToList();
                    if (existingComicsUnderNewName.Any()) {
                        var existingTitles = existingComicsUnderNewName.Select(c => c.Title).ToHashSet();
                        EnsureEmpty(
                            comics.Where(c => existingTitles.Contains(c.Title)),
                            $"The following items cannot be moved, because an item with the same title already exists under author {newAuthor}"
                        );
                    }

                    var count = 0;

                    var oldDirectories = new HashSet<string>();

                    // Step 2. Rename
                    // Note: we call this.Add/RemoveComics, and comics is selected from this.Comics, so we must call ToList() on comics
                    foreach (var comic in comics.ToList()) {
                        if (cc.IsCancellationRequested) {
                            return;
                        }

                        var categoryPath = this.Profile.RootPaths[comic.Category];
                        _ = oldDirectories.Add(Path.Combine(categoryPath, oldAuthor));
                        var oldPath = Path.Combine(categoryPath, oldAuthor, comic.Title);
                        var newPath = Path.Combine(categoryPath, newAuthor, comic.Title);

                        // some checks just in case
                        if (!IO.FileOrDirectoryExists(oldPath)) {
                            throw new ProgrammerError($"{nameof(this.StartRenameAuthorTaskAsync)}: comic not found at {oldPath}");
                        }

                        if (IO.FileOrDirectoryExists(newPath)) {
                            throw new ProgrammerError($"{nameof(this.StartRenameAuthorTaskAsync)}: comic already exists at {newPath}");
                        }

                        var newComic = comic.With(path: newPath, author: newAuthor);

                        IO.MoveDirectory(oldPath, newPath);

                        if (IO.FileOrDirectoryExists(Thumbnail.ThumbnailPath(newComic))) {
                            IO.RemoveFile(Thumbnail.ThumbnailPath(newComic));
                        }

                        IO.MoveFile(Thumbnail.ThumbnailPath(comic), Thumbnail.ThumbnailPath(newComic));

                        // Step 2.1. Update viewmodel
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            Windows.UI.Core.CoreDispatcherPriority.Normal,
                            async () => {
                                /* note: we directly remove the comic, instead of changing the comic's columns to reuse code.
                                 * it would probably be better if we directly edited database columns. */
                                await this.RemoveComicsAsync(new[] { comic });
                                _ = await this.AddComicsWithoutReplacingAsync(new[] { newComic }, notifyDuplicates: true);
                            }
                        );

                        p.Report(++count);
                    }

                    // step 3.
                    foreach (var old in oldDirectories) {
                        if (IO.GetDirectoryContents(old).Any()) {
                            throw new ProgrammerError("Not every file under directory {} was moved " +
                                "(the function should have terminated before reaching this point.)");
                        }

                        IO.RemoveDirectory(old);
                    }
                },
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );

            static void EnsureEmpty(IEnumerable<Comic> set, string errorMessage, string? title = null) {
                var comics = set.ToList();

                if (!comics.Any()) {
                    return;
                }

                var message = comics.Aggregate($"{errorMessage}", (message, comic) => message + "\n" + comic.UniqueIdentifier);
                throw new IntendedBehaviorException(message, title);
            }
        }

        #endregion

        #region Add, remove, modify comics

        /// <summary>
        /// returns the list of comics that were not added, because they already exist. 
        /// A successsful add where all comics were added would return an empty list.
        /// </summary>
        private async Task<List<Comic>> AddComicsWithoutReplacingAsync(IEnumerable<Comic> comics, bool notifyDuplicates = false) {
            var added = new ComicList();
            var duplicates = new List<Comic>();

            // we have to make a copy of comics, since the user might just pass this.comics (or more likely a query
            // based on it) to this function
            foreach (var comic in comics.ToList()) {
                if (!this.Comics.Contains(comic) && !added.Contains(comic)) {
                    added.Add(comic);
                } else {
                    duplicates.Add(comic);
                }
            }

            this.Comics.Add(added);

            var manager = await this.GetComicsManagerAsync();
            await manager.AddOrUpdateComicsAsync(added);

            if (notifyDuplicates && duplicates.Count > 0) {
                await this.NotifyComicsNotAddedAsync(duplicates);
            }

            return duplicates;
        }

        private async Task NotifyComicsNotAddedAsync(IEnumerable<Comic> comics) {
            var message = "The following items were not added because they already exist:\n";

            foreach (var comic in comics) {
                message += $"\n{comic.UniqueIdentifier}";
            }

            _ = await new ContentDialog { Content = message, Title = "Warning: items not added", CloseButtonText = "OK" }.ShowAsync();
        }

        private static async Task<bool> PromptRemoveComicsAsync(IEnumerable<Comic> comics) {
            var message = comics.Aggregate("The following items were not found on disk. Do you want them to be automatically removed from this library?", (current, comic) => current + $"\n{comic.UniqueIdentifier}");

            var result = await new ContentDialog {
                Title = "Warning: items not found",
                Content = message,
                PrimaryButtonText = "Remove",
                CloseButtonText = "Do not remove"
            }.ShowAsync();

            return result == ContentDialogResult.Primary;
        }

        public async Task RemoveComicsAsync(IEnumerable<Comic> comics) {
            var removed = comics.Where(comic => this.Comics.Contains(comic)).ToList();

            this.Comics.Remove(removed);

            var manager = await this.GetComicsManagerAsync();
            await manager.RemoveComicsAsync(removed);
        }

        public async Task UpdateComicAsync(IEnumerable<Comic> comics) {
            comics = comics.ToList();

            this.Comics.Modify(comics);

            var manager = await this.GetComicsManagerAsync();
            await manager.AddOrUpdateComicsAsync(comics);
        }

        public void NotifyThumbnailChanged(Comic comic) {
            this.Comics.NotifyThumbnailChanged(new[] { comic });
        }

        #endregion

        #region Providing features

        public Task UpdateTagNameAsync(string oldName, string newName) {
            var updatedComics = this.Comics
                .Where(c => c.Tags.Contains(oldName))
                .Select(c => c.WithMetadata(tags: c.Tags.Except(new[] { oldName }).Union(new[] { newName })))
                .ToList();

            return this.UpdateComicAsync(updatedComics);
        }

        public async Task RenameCategoryAsync(string oldName, string newName) {
            if (!this.Profile.RootPaths.TryGetValue(oldName, out var category)) { 
                throw new ProgrammerError($"category {oldName} does not exist");
            }

            if (this.Profile.RootPaths.ContainsName(newName)) {
                throw new ProgrammerError($"category {newName} already exists");
            }

            var updatedComics = this.Comics
                .Where(c => c.Category == oldName)
                .Select(c => c.With(category: newName))
                .ToList();

            _ = this.Profile.RootPaths.Remove(oldName);
            this.Profile.RootPaths.Add(newName, category.Path);

            await this.UpdateComicAsync(updatedComics);
            await ProfileManager.SaveProfileAsync(this.Profile);
        }

        #endregion
    }

    public class ProfileChangedEventArgs {
        public ProfileChangeType ChangeType { get; set; } = ProfileChangeType.ProfileChanged;
        public UserProfile NewProfile { get; set; } = new UserProfile();
    }

    public enum ProfileChangeType {
        ProfileChanged, SettingsChanged
    }

    public class NavigationRequestedEventArgs {
        public Type? PageType { get; set; }
        public NavigationTag? Tag { get; set; }
        public ComicView? Comics { get; set; }

        public NavigationTransitionInfo TransitionInfo { get; set; } = new EntranceNavigationTransitionInfo();
        public NavigationType NavigationType { get; set; } = NavigationType.New;
    }

    public enum NavigationType {
        Back, New, Scroll
    }
}
