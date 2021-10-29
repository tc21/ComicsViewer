using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsLibrary.SQL;
using ComicsLibrary.SQL.Sqlite;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.Uwp.Common;
using ComicsViewer.Uwp.Common.Win32Interop;
using Microsoft.Data.Sqlite;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class MainViewModel : ViewModelBase {
        internal readonly MainComicList Comics = new();
        public ComicView ComicView => this.Comics.Filtered();
        // TODO: we are currently using AggregateCollectionView, and casting IComicCollection to Playlist under the "promise"
        // that we'll only put playlists into this list.
        public AggregateCollectionView Playlists { get; } = new();

        public MainViewModel() {
            this.ProfileChanged += this.MainViewModel_ProfileChanged;
        }

        public string Title { get; private set; } = "Comics";

        public void SetPageName(string? value) {
            this.Title = value == null
                ? "Comics"
                : $"Comics - {value}";

            this.OnPropertyChanged(nameof(this.Title));
        }

        #region Profiles 

        internal UserProfile Profile = new() { Name = "<uninitialized>" };
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

            this.Comics.AbandonChildren();
            this.Comics.Filter.Clear();
            this.Playlists.Clear();

            // Note: it would probably be good practice to move static helper classes like ComicItemGridCache
            // and ConnectedAnimationHelper into instances contained in MainViewModel. Regardless, they are 
            // per-instance and per-profile, and have to be cleared when switching profiles.

            // Additionally, as more things get added (we already have playlists, filter, cache), it may be better
            // to invert the ownership hiearchy such that e.g. we pass `this` into CIGC, which listens to the ProfileChanged 
            // event, instead of the programmer "just knowing" that it needs to be cleared.
            ComicItemGridCache.Clear();
            ComicItemGridCache.IgnorePutsUntilNextGet();

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
            this.InvalidateComicItemCache();

            foreach (var playlist in await manager.GetPlaylistsAsync(this.Comics)) {
                this.Playlists.AddCollection(playlist);
            }

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

        public int NavigationDepth = 0;
        public NavigationTag ActiveNavigationTag;
        public NavigationPageType ActiveNavigationPageType;

        public void Navigate(NavigationTag navigationTag, NavigationTransitionInfo? transitionInfo = null, bool requireRefresh = false) {
            NavigationRequestedEventArgs navigationArguments;
            
            if (!requireRefresh && this.ActiveNavigationPageType is NavigationPageType.Root && this.ActiveNavigationTag == navigationTag) {
                navigationArguments = new NavigationRequestedEventArgs { NavigationType = NavigationType.ScrollToTop };
            } else {
                navigationArguments = new NavigationRequestedEventArgs {
                    NavigationType = NavigationType.New,
                    NavigationPageType = NavigationPageType.Root,
                    NavigationTag = navigationTag,
                    TransitionInfo = transitionInfo
                };
            }

            this.NavigationDepth = 0;
            ComicItemGridCache.PruneStack(0);

            this.NavigationRequested?.Invoke(this, navigationArguments);
        }

        public void TryNavigateOut() {
            if (this.NavigationDepth == 0) {
                return;
            }

            this.NavigationDepth -= 1;

            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs { NavigationType = NavigationType.Out });
        }

        public void NavigateInto(NavigationTag tag, string name) {
            if (tag is NavigationTag.Playlist) {
                
            }

            this.NavigateInto(this.NavigationItemFor(tag, name));
        }

        public void NavigateInto(ComicItem item) {
            var navigationPageType = item switch {
                ComicWorkItem _ => NavigationPageType.WorkItem,
                ComicNavigationItem _ => NavigationPageType.NavigationItem,
                _ => throw new ProgrammerError("Unexpected ComicItem type"),
            };

            this.NavigationDepth += 1;
            ComicItemGridCache.PruneStack(this.NavigationDepth - 1);

            this.NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs {
                NavigationPageType = navigationPageType,
                NavigationTag = this.ActiveNavigationTag,
                NavigationType = NavigationType.In,

                ComicItem = item,
                Title = item.Title
            });
        }

        public void FilterToSelected(IEnumerable<ComicItem> items) {
            this.FilterToComics(items.SelectMany(item => item.ContainedComics()));
        }

        public void NavigateToAuthor(string author) {
            var view = this.NavigationItemFor(NavigationTag.Author, author);
            this.NavigateInto(view);
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

        private string? lastSearch;

        // Called when a search is successfully compiled and submitted
        // Note: this happens at AppViewModel because filters are per-app
        public void SubmitSearch(Func<Comic, bool> search, string? searchText = null, bool rememberSearch = true) {
            searchText = searchText?.Trim();

            if (searchText != lastSearch) {
                lastSearch = searchText;
                this.Comics.Filter.Search = search;
            }

            if (rememberSearch && searchText is not null && searchText != "") {
                // Add this search to the recents list
                IList<string> savedSearches = Defaults.SettingsAccessor.GetSavedSearches(this.Profile.Name);
                RemoveIgnoreCase(ref savedSearches, searchText);
                savedSearches.Insert(0, searchText);
                Defaults.SettingsAccessor.SetSavedSearches(this.Profile.Name, savedSearches);
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
        public readonly ObservableCollection<ComicTask> Tasks = new();
        public bool IsTaskRunning => this.Tasks.Count > 0;
        private readonly Dictionary<string, ComicTask> taskNames = new();

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
                        }.ScheduleShowAsync();
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
                asyncCallback: asyncCallback == null ? null : (_ => asyncCallback()),
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
                }.ScheduleShowAsync();
            }
        }

        public async Task StartReloadAllComicsTaskAsync() {
            await this.StartUniqueTaskAsync("reload", "Reloading all comics...", 
                async (cc, p) => await ComicsLoader.FromProfilePathsAsync(this.Profile).WithProgressAsync(p, cc).ToListAsync(),
                async result => {
                    // TODO: we should probably figure out how to only update what we need
                    var manager = await this.GetComicsManagerAsync();
                    result = await manager.RetrieveKnownMetadataAsync(result);

                    // A reload all completely replaces the current comics list. We won't send any events. We'll just refresh everything.
                    // TODO: remove thumbnails on comics that didn't get reloaded
                    await this.RemoveComicsAsync(this.Comics);
                    _ = await this.AddComicsWithoutReplacingAsync(result, notifyDuplicates: true);
                }, 
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public async Task StartReloadCategoryTaskAsync(NamedPath category) {
            await this.StartUniqueTaskAsync("reload", $"Reloading category '{category.Name}'...",
                async (cc, p) => await ComicsLoader.FromRootPathAsync(this.Profile, category).WithProgressAsync(p, cc).ToListAsync(),
                async result => {
                    var manager = await this.GetComicsManagerAsync();
                    result = await manager.RetrieveKnownMetadataAsync(result);

                    // TODO: remove thumbnails on comics that didn't get reloaded
                    await this.RemoveComicsAsync(this.Comics.Where(comic => comic.Category == category.Name));
                    _ = await this.AddComicsWithoutReplacingAsync(result, notifyDuplicates: true);
                },
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public async Task StartLoadComicsFromFoldersTaskAsync(IEnumerable<StorageFolder> folders) {
            var comicFolders = folders.ToList();

            await this.StartUniqueTaskAsync("reload", $"Adding comics from {comicFolders.Count} folders...",
                async (cc, p) => await ComicsLoader.FromImportedFoldersAsync(this.Profile, comicFolders).WithProgressAsync(p, cc).ToListAsync(),
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
                            await this.RemoveComicsAsync(result, deleteThumbnails: true);
                        }
                    }
                },
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        // TODO: figure out where to move these functions
        private async Task MoveComicsAsync(
            List<Comic> oldComics, List<Comic> newComics, CancellationToken ct, IProgress<int>? progress
        ) {
            EnsureEmpty(
                oldComics.Where(c => !IO.FileOrDirectoryExists(c.Path)),
                "The following items could not be found"
            );

            EnsureEmpty(
                newComics.Where((comic, index) => oldComics[index].Path != comic.Path && IO.FileOrDirectoryExists(comic.Path)),
                $"The following items cannot be moved, because an item already exists at its target path"
            );

            var counter = 0;
            var parentDirectories = new HashSet<string>();

            foreach (var (oldComic, newComic) in oldComics.Zip(newComics, (a, b) => (a, b))) {
                ct.ThrowIfCancellationRequested();

                if (oldComic.Path != newComic.Path) {
                    // last-minute checks just in case
                    if (!IO.FileOrDirectoryExists(oldComic.Path)) {
                        throw new ProgrammerError($"{nameof(this.MoveComicsAsync)}: comic not found at {oldComic.Path}");
                    }

                    if (IO.FileOrDirectoryExists(newComic.Path)) {
                        throw new ProgrammerError($"{nameof(this.MoveComicsAsync)}: comic already exists at {newComic.Path}");
                    }

                    IO.MoveDirectory(oldComic.Path, newComic.Path);
                }

                // remove parent folder if possible
                var authorFolder = Path.GetDirectoryName(oldComic.Path);

                if (!IO.GetDirectoryContents(authorFolder).Any()) {
                    IO.RemoveDirectory(authorFolder);
                }

                IEnumerable<string>? playlists = null;

                if (oldComic.UniqueIdentifier != newComic.UniqueIdentifier) {
                    if (IO.FileOrDirectoryExists(Thumbnail.ThumbnailPath(newComic))) {
                        IO.RemoveFile(Thumbnail.ThumbnailPath(newComic));
                    }

                    IO.MoveFile(Thumbnail.ThumbnailPath(oldComic), Thumbnail.ThumbnailPath(newComic));

                    // This is the only place where a comic's UniqueId can possibly change.
                    // We also need to handle playlists. Elsewhere we pretend we removed an item and added a new one.

                    // We must call ToList to eagerly evaluate this query before the update happens below.
                    playlists = this.Playlists.Where(playlist => playlist.Comics.Contains(oldComic))
                        .Select(playlist => playlist.Name)
                        .ToList();
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    async () => {
                        if (oldComic.UniqueIdentifier == newComic.UniqueIdentifier) {
                            await this.UpdateComicAsync(new[] { newComic });
                        } else {
                            await this.RemoveComicsAsync(new[] { oldComic });
                            _ = await this.AddComicsWithoutReplacingAsync(new[] { newComic });
                        }

                        if (oldComic.UniqueIdentifier != newComic.UniqueIdentifier) {
                            foreach (var playlist in playlists!) {
                                // this playlist might have been deleted in the above operation
                                // note: really this should be fixed, since if that happened the user would also be navigated out of the playlist
                                if (this.Playlists.ContainsKey(playlist)) {
                                    await this.AddToPlaylistAsync(playlist, new[] { newComic });
                                } else {
                                    await this.CreatePlaylistAsync(playlist, new[] { newComic });
                                }
                            }
                        }
                    }
                );

                /* Design note: when we designed the app, we assumed we'd never change UniqueId. The above playlist
                 * code, for example, is code that would be unnecessary if the rest of the app doesn't assume UniqueId
                 * never changes. The else clause in the update code, for example, is required because (1) ComicList
                 * does not have a way to update a comic's UniqueId, and (2) the Sqlite database cannot update a comic's UniqueId. */

                counter += 1;
                progress?.Report(counter);
            }

            static void EnsureEmpty(IEnumerable<Comic> set, string errorMessage, string? title = null) {
                var comics = set.ToList();

                if (!comics.Any()) {
                    return;
                }

                var message = comics.Aggregate($"{errorMessage}", (message, comic) => message + "\n" + comic.UniqueIdentifier);
                throw new IntendedBehaviorException(message, title);
            }
        }

        public Task StartMoveComicsTaskAsync(IEnumerable<Comic> oldComics, IEnumerable<Comic> newComics) {
            var oldComics_ = oldComics.ToList();
            var newComics_ = newComics.ToList();

            return this.StartUniqueTaskAsync(
                "moveFiles",
                $"Moving {oldComics_.Count.PluralString("item")}...",
                (ct, p) => this.MoveComicsAsync(oldComics_, newComics_, ct, p),
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public Task StartRenameAuthorTaskAsync(string oldAuthor, string newAuthor) {
            var oldComics = this.Comics.Where(c => c.Author == oldAuthor).ToList();
            var oldDirectories = new HashSet<string>();
            var newComics = oldComics.Select(comic => {
                var categoryPath = Path.GetDirectoryName(Path.GetDirectoryName(comic.Path));
                _ = oldDirectories.Add(Path.Combine(categoryPath, oldAuthor));
                var oldPath = Path.Combine(categoryPath, oldAuthor, comic.Title);
                var newPath = Path.Combine(categoryPath, newAuthor, comic.Title);
                return comic.With(path: newPath, author: newAuthor);
            }).ToList();

            return this.StartUniqueTaskAsync(
                "moveFiles", 
                $"Moving {oldComics.Count.PluralString("item")} belonging to author {oldAuthor}...",

                async (ct, p) => {
                    // validation happens here to take advantage of StartUniqueTaskAsync's exception handler
                    if (!newAuthor.IsValidFileName()) {
                        throw new IntendedBehaviorException("new author name contains invalid filename characters");
                    }

                    await this.MoveComicsAsync(oldComics, newComics, ct, p);

                    // validate again before we return
                    if (oldDirectories.Any(IO.FileOrDirectoryExists)) {
                        throw new ProgrammerError("Not every file under directory {} was moved " +
                            "(the function should have terminated before reaching this point.)");
                    }
                },

                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        public Task StartMoveComicsToCategoryTaskAsync(IEnumerable<Comic> comics, NamedPath category) {
            var oldComics = comics.ToList();
            var newComics = oldComics.Select(comic => {
                var originalAuthorPath = Path.GetDirectoryName(comic.Path);
                var targetPath = Path.Combine(category.Path, comic.Author, comic.Title);
                return comic.With(path: targetPath, category: category.Name);
            }).ToList();

            return this.StartUniqueTaskAsync(
                "moveFiles",
                $"Moving {oldComics.Count.PluralString("item")} to category '{category.Name}'...",

                async (ct, p) => {
                    try {
                        _ = await StorageFolder.GetFolderFromPathAsync(category.Path);
                    } catch (FileNotFoundException) {
                        throw new IntendedBehaviorException($"The destination folder {category.Path} does not exist. You must create it manually.", "Folder not found");
                    }

                    await this.MoveComicsAsync(oldComics, newComics, ct, p);
                }, 
                
                exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptionsAsync
            );
        }

        #endregion

        #region Add, remove, modify comics

        /* The Comic Item Grid Cache currently is unable to keep up with additions and deletions. 
         * We keep track of when an item was last added or deleted, so we can invalidate the cache when needed.
         * This is not an ideal solution. */
        public DateTime LastModified { get; private set; } = DateTime.MinValue;

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

            // this update must happen before this.Comics.Add because many view models listen to its events
            this.LastModified = DateTime.Now;
            this.Comics.Add(added);

            // Adding any item will cause our cache to need to be updated.
            // Considering adding items is a rarer occurence than navigation, we'll just invalidate the entire cache
            this.InvalidateComicItemCache();

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

            _ = await new ContentDialog { Content = message, Title = "Warning: items not added", CloseButtonText = "OK" }.ScheduleShowAsync();
        }

        private static async Task<bool> PromptRemoveComicsAsync(IEnumerable<Comic> comics) {
            var message = comics.Aggregate("The following items were not found on disk. Do you want them to be automatically removed from this library?", (current, comic) => current + $"\n{comic.UniqueIdentifier}");

            var result = await new ContentDialog {
                Title = "Warning: items not found",
                Content = message,
                PrimaryButtonText = "Remove",
                CloseButtonText = "Do not remove"
            }.ScheduleShowAsync();

            return result == ContentDialogResult.Primary;
        }

        public async Task RemoveComicsAsync(IEnumerable<Comic> comics, bool deleteThumbnails = false) {
            var removed = comics.Where(comic => this.Comics.Contains(comic)).ToList();

            // this update must happen before this.Comics.Add because many view models listen to its events
            this.LastModified = DateTime.Now;
            this.Comics.Remove(removed);

            var manager = await this.GetComicsManagerAsync();
            await manager.RemoveComicsAsync(removed);

            if (deleteThumbnails) {
                foreach (var comic in removed) {
                    var thumbnailPath = Thumbnail.ThumbnailPath(comic);

                    if (IO.FileOrDirectoryExists(thumbnailPath)) {
                        IO.RemoveFile(thumbnailPath);
                    }
                }
            }
        }

        public async Task UpdateComicAsync(IEnumerable<Comic> comics) {
            comics = comics.ToList();

            this.Comics.Modify(comics);
            this.InvalidateComicItemCache();

            var manager = await this.GetComicsManagerAsync();
            await manager.AddOrUpdateComicsAsync(comics);
        }

        public enum PlaylistChangeType {
            Add, Remove
        }

        public delegate void PlaylistChangedHandler(MainViewModel source, PlaylistChangedArguments e);
        public event PlaylistChangedHandler? PlaylistChanged;
        public class PlaylistChangedArguments {
            public PlaylistChangeType Type { get; }
            public Playlist Playlist { get; }

            public PlaylistChangedArguments(PlaylistChangeType type, Playlist playlist) {
                this.Type = type;
                this.Playlist = playlist;
            }
        }

        public void NotifyThumbnailChanged(Comic comic) {
            this.Comics.NotifyThumbnailChanged(new[] { comic });
        }

        // Helper functions accessible from anywhere
        public async Task TryRedefineComicThumbnailAsync(Comic comic, StorageFile thumbnailFile) {
            var newComic = comic.WithMetadata(thumbnailSource: thumbnailFile.RelativeTo(comic.Path));

            var success = await Thumbnail.GenerateThumbnailFromStorageFileAsync(newComic, thumbnailFile, this.Profile, replace: true);
            if (success) {
                this.NotifyThumbnailChanged(comic);
                await this.UpdateComicAsync(new[] { newComic });
            }
        }

        public async Task TryRedefineComicThumbnailFromFilePickerAsync(Comic comic) {
            var picker = new FileOpenPicker {
                ViewMode = PickerViewMode.Thumbnail
            };

            foreach (var extension in UserProfile.ImageFileExtensions) {
                picker.FileTypeFilter.Add(extension);
            }

            var file = await picker.PickSingleFileAsync();

            if (file == null) {
                return;
            }

            await this.TryRedefineComicThumbnailAsync(comic, file);
        }

        #endregion

        #region Retrieving ComicItem instances 



        // Note: we haven't tested if the cache is actually needed for performance, so maybe we should test it
        private readonly Dictionary<string, ComicWorkItem> comicWorkItems = new();
        private readonly Dictionary<NavigationTag, ComicPropertiesCollectionView> comicPropertyCollections = new();
        private readonly Dictionary<NavigationTag, Dictionary<string, ComicNavigationItem>> comicNavigationItems = new();

        private void InvalidateComicItemCache() {
            this.comicWorkItems.Clear();
            this.comicPropertyCollections.Clear();
            this.comicNavigationItems.Clear();
        }

        public ComicWorkItem WorkItemFor(Comic comic) {
            if (!this.comicWorkItems.TryGetValue(comic.UniqueIdentifier, out var item)) {
                item = new ComicWorkItem(comic, this.Comics);
                item.RequestingRefresh += this.ComicWorkItem_RequestingRefresh;
                this.comicWorkItems.Add(comic.UniqueIdentifier, item);
            }

            return item;
        }

        public ComicPropertiesCollectionView SortedComicCollectionsFor(NavigationTag navigationTag, ComicCollectionSortSelector? sortSelector) {
            if (!this.comicPropertyCollections.TryGetValue(navigationTag, out var view)) {
                view = this.Comics.SortedProperties(
                    navigationTag switch {
                        NavigationTag.Author => comic => new[] { comic.Author },
                        NavigationTag.Category => comic => new[] { comic.Category },
                        NavigationTag.Tags => comic => comic.Tags,
                        _ => throw new ProgrammerError($"unsupported navigation tag ({navigationTag})")
                    },
                    sortSelector ?? default
                );

                this.comicPropertyCollections.Add(navigationTag, view);
            } else if (sortSelector is { } selector) {
                view.SetSort(selector);
            }

            return view;
        }

        public List<ComicNavigationItem> NavigationItemsFor(NavigationTag tag, ComicCollectionSortSelector? sortSelector = null) {
            ComicCollectionView collectionsView = tag switch {
                NavigationTag.Comics => throw new ProgrammerError("Cannot create navigation items for root page"),
                NavigationTag.Playlist => this.Playlists,
                _ => this.SortedComicCollectionsFor(tag, sortSelector)
            };

            return collectionsView.Select(collection => this.GetOrMakeNavigationItem(tag, collection.Name, collection.Comics)).ToList();
        }

        public ComicNavigationItem NavigationItemFor(NavigationTag tag, string name) {
            var comics = tag switch {
                NavigationTag.Comics => throw new ProgrammerError("Cannot create navigation items for root page"),
                NavigationTag.Playlist => this.Playlists.GetCollection(name).Comics,
                _ => this.SortedComicCollectionsFor(tag, null).GetView(name),
            };

            return this.GetOrMakeNavigationItem(tag, name, comics);
        }

        private ComicNavigationItem GetOrMakeNavigationItem(NavigationTag tag, string name, ComicView comics) {
            if (!this.comicNavigationItems.TryGetValue(tag, out var items)) {
                items = new();
                this.comicNavigationItems.Add(tag, items);
            }

            if (!items.TryGetValue(name, out var item)) {
                item = new ComicNavigationItem(name, comics);
                items.Add(name, item);
            }

            return item;
        }

        private void ComicWorkItem_RequestingRefresh(ComicWorkItem sender, ComicWorkItem.RequestingRefreshType type) {
            switch (type) {   // switch RequestingRefreshType
                // TODO: RequestingRefreshType now only hase one case. Evaluate if it needs to be removed
                case ComicWorkItem.RequestingRefreshType.Remove:
                    _ = this.comicWorkItems.Remove(sender.Comic.UniqueIdentifier);
                    break;

                default:
                    throw new ProgrammerError("Unhandled switch case");
            }
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

        public async Task RemoveFromPlaylistAsync(string playlistName, IEnumerable<Comic> comics) {
            if (!this.Playlists.ContainsKey(playlistName)) {
                throw new ProgrammerError($"Adding to nonexistent playlist should not be possible (tried to remove from playlist '{playlistName}')");
            }

            var playlist = (Playlist)this.Playlists.GetCollection(playlistName);

            playlist.ExceptWith(comics);

            var manager = await this.GetComicsManagerAsync();
            await manager.RemoveComicsFromPlaylistAsync(playlistName, comics);
        }

        public async Task AddToPlaylistAsync(string playlistName, IEnumerable<Comic> comics) {
            if (!this.Playlists.ContainsKey(playlistName)) {
                throw new ProgrammerError($"Adding to nonexistent playlist should not be possible (tried to add to playlist '{playlistName}')");
            }

            var playlist = (Playlist)this.Playlists.GetCollection(playlistName);

            playlist.UnionWith(comics);

            var manager = await this.GetComicsManagerAsync();
            await manager.AddComicsToPlaylistAsync(playlistName, comics);
        }

        public async Task DeletePlaylistAsync(string playlistName, bool updateDatabase = true) {
            if (!this.Playlists.ContainsKey(playlistName)) {
                throw new ProgrammerError($"Deleting a nonexistent playlist should not be possible (tried to delete playlist '{playlistName}')");
            }

            var playlist = (Playlist)this.Playlists.GetCollection(playlistName);
            this.Playlists.RemoveCollection(playlist);
            this.PlaylistChanged?.Invoke(this, new PlaylistChangedArguments(PlaylistChangeType.Remove, playlist));

            if (!updateDatabase) {
                return;
            }

            var manager = await this.GetComicsManagerAsync();
            await manager.RemovePlaylistAsync(playlist.Name);
            
        }

        public async Task CreatePlaylistAsync(string name, IEnumerable<Comic>? comics = null, bool updateDatabase = true) {
            if (this.Playlists.ContainsKey(name)) {
                throw new ArgumentException($"playlist {name} already exists");
            }

            var uniqueIds = comics is null ? new string[] { } : comics.Select(comic => comic.UniqueIdentifier);
            var playlist = Playlist.Make(this.Comics, name, uniqueIds);
            this.Playlists.AddCollection(playlist);
            this.PlaylistChanged?.Invoke(this, new PlaylistChangedArguments(PlaylistChangeType.Add, playlist));

            if (!updateDatabase) {
                return;
            }

            var manager = await this.GetComicsManagerAsync();
            await manager.AddPlaylistAsync(name);
            if (comics != null) {
                await manager.AddComicsToPlaylistAsync(name, comics);
            }
        }

        public async Task RenamePlaylistAsync(string oldName, string newName) {
            if (!this.Playlists.ContainsKey(oldName)) {
                throw new ProgrammerError($"playlist {oldName} does not exist");
            }

            var playlist = this.Playlists.GetCollection(oldName);

            if (this.Playlists.ContainsKey(newName)) {
                throw new ProgrammerError($"playlist {newName} already exists");
            }

            await this.DeletePlaylistAsync(oldName, updateDatabase: false);
            await this.CreatePlaylistAsync(newName, playlist.Comics, updateDatabase: false);
            var manager = await this.GetComicsManagerAsync();
            await manager.RenamePlaylistAsync(oldName, newName);
        }

        #endregion


        public string GetProposedPlaylistName() {
            return GetProposedPlaylistNames().Where(name => !this.Playlists.ContainsKey(name)).First();
        }

        private static IEnumerable<string> GetProposedPlaylistNames() {
            yield return "New Playlist";

            for (var i = 1; /* forever */; i++) {
                yield return $"New Playlist {i}";
            }
        }
    }

    public class ProfileChangedEventArgs {
        public ProfileChangeType ChangeType { get; set; } = ProfileChangeType.ProfileChanged;
        public UserProfile NewProfile { get; set; } = new();
    }

    public enum ProfileChangeType {
        ProfileChanged, SettingsChanged
    }

    public class NavigationRequestedEventArgs {
        public NavigationType NavigationType { get; set; }
        public NavigationPageType NavigationPageType { get; set; }

        public NavigationTag NavigationTag { get; set; }
        public NavigationTransitionInfo? TransitionInfo { get; set; }
        public ComicItemGridViewModelProperties? Properties { get; set; }

        public string? Title { get; set; }
        public ComicItem? ComicItem { get; set; }
    }

    public enum NavigationType {
        In, Out, New, ScrollToTop
    }

    public enum NavigationPageType {
        Root, NavigationItem, WorkItem
    }
}
