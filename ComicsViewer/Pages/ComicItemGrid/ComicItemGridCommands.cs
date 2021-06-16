using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.Uwp.Common;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;


#nullable enable

namespace ComicsViewer.Pages {
    using ComicItemGridCommand = ComicItemGridCommand<ComicItemGridViewModel, ComicItem>;
    using ComicNavigationItemGridCommand = ComicItemGridCommand<ComicNavigationItemGridViewModel, ComicNavigationItem>;
    using ComicWorkItemGridCommand = ComicItemGridCommand<ComicWorkItemGridViewModel, ComicWorkItem>;

    public partial class ComicItemGrid {
        /* A note on keyboard shortcuts: KeyboardAccelerators seem to only run when the control responsible for the 
         * command is available. This translates to shortcuts only working then the command's CanExecute evaluated to 
         * true the last time the context menu flyout was shown. Since we can't get commands to consistently execute 
         * without rewriting the system, they are disabled for now. */
        private ComicItemGridCommands? contextMenuCommands;
        public ComicItemGridCommands ContextMenuCommands {
            get {
                this.contextMenuCommands ??= new ComicItemGridCommands(this);
                return this.contextMenuCommands;
            }
        }

        public Task<ContentDialogResult> ShowConfirmRemoveItemDialogAsync()
            => this.ConfirmRemoveItemDialog.ScheduleShowAsync();

        #region Supporting classes

        // This class defined within ComicItemGrid to have access to VisibleComicsGrid
        public class CommandArgs<T, TItem> where T : ComicItemGridViewModel where TItem : ComicItem {
            public ComicItemGrid Grid { get; }
            public T ViewModel { get; }
            public MainViewModel MainViewModel { get; }
            public int Count { get; }
            public IEnumerable<TItem> Items { get; }
            public bool IsWorkItems { get; }
            public bool IsNavItems => !this.IsWorkItems;
            public int ComicCount { get; }
            public NavigationTag NavigationTag { get; }
            public NavigationPageType NavigationPageType { get; }

            public CommandArgs(ComicItemGrid grid) {
                if (grid.ViewModel is not T) {
                    throw new ProgrammerError($"A wrong {nameof(CommandArgs<T, TItem>)} was created: incorrect view model type");
                }

                this.Grid = grid;

                // Note: these properties are generated in the constructor, instead of dynamically, 
                // since operations within commands may be able to change grid selections
                this.ViewModel = (T)grid.ViewModel;
                this.MainViewModel = grid.MainViewModel;
                this.Count = grid.VisibleComicsGrid.SelectedItems.Count;
                this.Items = grid.VisibleComicsGrid.SelectedItems.Cast<TItem>().ToList();
                this.IsWorkItems = this.ViewModel is ComicWorkItemGridViewModel;
                this.ComicCount = this.Items.SelectMany(i => i.ContainedComics()).Count();
                this.NavigationTag = grid.ViewModel.NavigationTag;
                this.NavigationPageType = grid.ViewModel.NavigationPageType;
            }
        }

        #endregion

        #region Dynamic context menu items

        private void ComicItemGridContextFlyout_Opening(object sender, object e) {
            if (sender is not MenuFlyout flyout) {
                throw new ProgrammerError("Only MenuFlyout should be allowed to call this handler");
            }

            // Update dynamic text when opening flyout
            _ = UpdateFlyoutItems(flyout.Items!);

            bool UpdateFlyoutItems(IEnumerable<MenuFlyoutItemBase> flyoutItems) {
                var anyItemsEnabled = false;
                var showNextSeparator = false;
                foreach (var item in flyoutItems) {
                    switch (item) {
                        case MenuFlyoutSubItem subitem when subitem == GoToPlaylistFlyoutMenuSubitem:
                            if (this.ViewModel is not ComicWorkItemGridViewModel) {
                                subitem.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                                break;
                            }

                            var items = this.VisibleComicsGrid.SelectedItems.Cast<ComicWorkItem>().Select(item => item.Comic);
                            var playlists = this.MainViewModel.Playlists.Where(playlist => items.Any(playlist.Comics.Contains)).ToList();

                            if (!playlists.Any()) {
                                subitem.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                                break;
                            }

                            subitem.Visibility = Windows.UI.Xaml.Visibility.Visible;
                            subitem.Items.Clear();

                            foreach (var playlist in playlists) {
                                // note: we might want to consider moving this logic to MainViewModel
                                var flyoutItem = new ComicsMenuFlyoutItem { Text = playlist.Name };
                                flyoutItem.Click += (a, e) => {
                                    var view = this.MainViewModel.NavigationItemFor(NavigationTag.Playlist, playlist.Name);
                                    this.MainViewModel.NavigateInto(view);
                                };
                                subitem.Items.Add(flyoutItem);
                            }

                            break;

                        case MenuFlyoutSubItem subitem:
                            if (UpdateFlyoutItems(subitem.Items!)) {
                                subitem.Visibility = Windows.UI.Xaml.Visibility.Visible;
                                showNextSeparator = true;
                            } else {
                                subitem.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                            }

                            break;

                        case ComicsMenuFlyoutItem flyoutItem when flyoutItem.Command is IManuallyManagedCommand command:
                            if (command.CanExecute()) {
                                flyoutItem.Text = command.GetName();
                                flyoutItem.Visibility = Windows.UI.Xaml.Visibility.Visible;
                                anyItemsEnabled = true;
                                showNextSeparator = true;
                            } else {
                                flyoutItem.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                            }

                            break;

                        case MenuFlyoutSeparator separator:
                            separator.Visibility = showNextSeparator
                                ? Windows.UI.Xaml.Visibility.Visible
                                : Windows.UI.Xaml.Visibility.Collapsed;

                            showNextSeparator = false;

                            break;
                    }
                }

                return anyItemsEnabled;
            }
        }

        #endregion
    }

    public interface IManuallyManagedCommand {
        public Func<string> GetName { get; }
        public Func<bool> CanExecute { get; }
    }

    // Note: we are experimenting with removing CanExecuteRequested to enable keyboard shortcuts.
    public class ComicItemGridCommand<T, TItem> : XamlUICommand, IManuallyManagedCommand where T : ComicItemGridViewModel where TItem : ComicItem {
        public Func<string> GetName { get; }
        public Func<bool> CanExecute { get; }

        public ComicItemGridCommand(
            ComicItemGrid grid,
            Func<ComicItemGrid.CommandArgs<T, TItem>, string> getName,
            Action<ComicItemGrid.CommandArgs<T, TItem>> execute, 
            Func<ComicItemGrid.CommandArgs<T, TItem>, bool>? canExecute = null
        ) {
            this.GetName = () => getName(new ComicItemGrid.CommandArgs<T, TItem>(grid));
            this.CanExecute = () => {
                if (grid.ViewModel is not T) {
                    return false;
                }

                var args = new ComicItemGrid.CommandArgs<T, TItem>(grid);
                return canExecute?.Invoke(args) ?? true;
            };

            this.ExecuteRequested += (sender, args) => {
                if (this.CanExecute()) {
                    execute(new ComicItemGrid.CommandArgs<T, TItem>(grid));
                }
            };
        }

        public ComicItemGridCommand(
            ComicItemGrid grid,
            string name,
            Action<ComicItemGrid.CommandArgs<T, TItem>> execute,
            Func<ComicItemGrid.CommandArgs<T, TItem>, bool>? canExecute = null
        ) : this(grid, (_) => name, execute, canExecute) { }
    }

    public class ComicItemGridCommands {
        /* Since a StandardUICommand has an icon, but XamlUICommand doesn't, this is a good way to see which of 
         * our commands already have an icon and which ones need one defined in XAML */
        public ComicItemGridCommand SearchSelectedCommand { get; }
        public ComicItemGridCommand RemoveItemCommand { get; }
        public ComicItemGridCommand MoveFilesCommand { get; }
        public ComicItemGridCommand EditItemCommand { get; }
        public ComicItemGridCommand AddToPlaylistCommand { get; }

        public ComicWorkItemGridCommand OpenItemsCommand { get; }
        public ComicWorkItemGridCommand ShowInExplorerCommand { get; }
        public ComicWorkItemGridCommand GenerateThumbnailCommand { get; }
        public ComicWorkItemGridCommand RedefineThumbnailCommand { get; }
        public ComicWorkItemGridCommand LoveComicsCommand { get; }
        public ComicWorkItemGridCommand SearchAuthorCommand { get; }
        public ComicWorkItemGridCommand RemoveFromSelectedPlaylistCommand { get; }

        public ComicNavigationItemGridCommand NavigateIntoCommand { get; }

        public ComicNavigationItemGridCommand CreatePlaylistCommand { get; }
        public ComicNavigationItemGridCommand DeletePlaylistCommand { get; }

        private static string DescribeItem(string action, int count)
            => count == 1 ? action : $"{action} {count} items";

        internal ComicItemGridCommands(ComicItemGrid parent) {
            // Opens selected comics
            this.OpenItemsCommand = new ComicWorkItemGridCommand(parent,
                getName: e => DescribeItem("Open", e.ComicCount),
                execute: async e => await e.ViewModel.OpenItemsAsync(e.Items),
                canExecute: e => e.Count > 0
            );

            // Generates and executes a search limiting visible items to those selected
            this.SearchSelectedCommand = new ComicItemGridCommand(parent,
                name: "Search selected",
                execute: e => e.MainViewModel.FilterToSelected(e.Items),
                canExecute: e => (e.IsNavItems && e.Count > 0) || e.Count > 1
            );

            // Removes comics by asking the view model to do it for us
            this.RemoveItemCommand = new ComicItemGridCommand(parent,
                getName: e => DescribeItem("Remove", e.ComicCount),
                execute: async e => {
                    if (await e.Grid.ShowConfirmRemoveItemDialogAsync() != ContentDialogResult.Primary) {
                        return;
                    }

                    var comics = e.Items.SelectMany(item => item.ContainedComics()).ToList();
                    await e.MainViewModel.RemoveComicsAsync(comics, deleteThumbnails: true);
                },
                canExecute: e => e.Count > 0
            );

            // Opens a flyout to move items between categories
            this.MoveFilesCommand = new ComicItemGridCommand(parent,
                getName: e => DescribeItem("Move", e.ComicCount),
                execute: async e => await e.Grid.ShowMoveFilesDialogAsync(e.Items),
                canExecute: e => e.Count > 0
            );

            // For work items: opens the comic info flyout to the "Edit Info" page
            // For nav items: Renames a tag, etc.
            // Really, this should be two commands, but we can't use the same keyboard shortuct for multiple comands unless
            // we write some custom logic during ComicItemGrid creation...
            // Actually, on second thought, it's probably pretty easy. Just do it in ContextMenuCommands { get; }.
            // TODO (and remember to look at the shortcuts for Open/NavigateInto)
            this.EditItemCommand = new ComicItemGridCommand(parent,
                getName: e => e.IsWorkItems ? "Edit info" : $"Rename {e.ViewModel.NavigationTag.Describe()}",
                execute: async e => {
                    if (e.IsWorkItems) {
                        await e.Grid.ShowEditComicInfoDialogAsync((ComicWorkItem)e.Items.First());
                    } else {
                        await e.Grid.ShowEditNavigationItemDialogAsync((ComicNavigationItem)e.Items.First());
                    }
                },
                canExecute: e => e.Count == 1
            );

            // Opens the containing folder in Windows Explorer
            this.ShowInExplorerCommand = new ComicWorkItemGridCommand(parent,
                getName: e => DescribeItem("Show", e.Count) + " in Explorer",
                execute: e => {
                    foreach (var item in e.Items) {
                        _ = Startup.OpenContainingFolderAsync(item.Comic);
                    }
                },
                canExecute: e => e.Count > 0
            );

            // Opens the containing folder in Windows Explorer
            this.GenerateThumbnailCommand = new ComicWorkItemGridCommand(parent,
                getName: e => e.Count == 1 ? "Generate thumbnail" : $"Generate thumbnails for {e.Count} items",
                execute: e => e.ViewModel.ScheduleGenerateThumbnails(e.Items.Select(item => item.Comic), replace: true),
                canExecute: e => e.Count > 0
            );

            // Opens the comic info flyout to the "Edit Info" page
            this.RedefineThumbnailCommand = new ComicWorkItemGridCommand(parent,
                name: "Redefine thumbnail",
                execute: async e => await e.Grid.RedefineThumbnailAsync(e.Items.First()),
                canExecute: e => e.Count == 1
            );

            // Loves, or unloves comics
            this.LoveComicsCommand = new ComicWorkItemGridCommand(parent,
                getName: e => e.Items.All(i => i.IsLoved) ? DescribeItem("No longer love", e.Count) : DescribeItem("Love", e.Count),
                execute: async e => await e.ViewModel.ToggleLovedStatusForComicsAsync(e.Items),
                canExecute: e => e.Count > 0
            );

            this.SearchAuthorCommand = new ComicWorkItemGridCommand(parent,
                getName: e => $"Show all items by {e.Items.First().Comic.Author}",
                execute: e => {
                    var item = e.Items.First();
                    e.Grid.PrepareNavigateIn(item);
                    e.MainViewModel.NavigateToAuthor(item.Comic.Author);
                },
                canExecute: e => e.Count == 1
            );


            // Navigates into the selected navigation item
            this.NavigateIntoCommand = new ComicNavigationItemGridCommand(parent,
                name: "Navigate into",
                execute: e => {
                    var item = e.Items.First();
                    e.Grid.PrepareNavigateIn(item);
                    e.MainViewModel.NavigateInto(item);
                },
                canExecute: e => e.Count == 1
            );

            // Creates a playlist
            this.CreatePlaylistCommand = new ComicNavigationItemGridCommand(parent,
                name: $"Create playlist...",
                execute: async e => await e.Grid.ShowCreatePlaylistDialogAsync(),
                canExecute: e => e.NavigationTag == NavigationTag.Playlist
            );

            // Removes a playlist
            this.DeletePlaylistCommand = new ComicNavigationItemGridCommand(parent,
                getName: e => $"Delete {e.Count.PluralString("playlist", simple: true)}",
                execute: async e => {
                    foreach (var item in e.Items) {
                        await e.MainViewModel.DeletePlaylistAsync(item.Title);
                    }
                },
                canExecute: e => e.NavigationTag == NavigationTag.Playlist && e.Count > 0
            );

            // Popup dialog to add to playlist
            this.AddToPlaylistCommand = new ComicItemGridCommand(parent,
                name: "Add to playlist...",
                execute: async e => await e.Grid.ShowAddItemsToPlaylistDialogAsync(e.Items),
                canExecute: e => e.Count > 0
            );

            // Removes an item from the currently active playlist
            this.RemoveFromSelectedPlaylistCommand = new ComicWorkItemGridCommand(parent,
                getName: e => $"Remove from playlist '{e.ViewModel.Properties.PlaylistName}'", 
                execute: async e => await e.MainViewModel.RemoveFromPlaylistAsync(e.ViewModel.Properties.PlaylistName!, e.Items.Select(item => item.Comic)),
                canExecute: e => e.Count > 0 && e.ViewModel.Properties.ParentType == NavigationTag.Playlist
            );

        }
    }
}
