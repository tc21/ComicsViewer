using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;


#nullable enable

namespace ComicsViewer.Pages {
    using ComicItemGridCommand = ComicItemGridCommand<ComicItemGridViewModel, ComicItem>;
    using ComicWorkItemGridCommand = ComicItemGridCommand<ComicWorkItemGridViewModel, ComicWorkItem>;
    using ComicNavigationItemGridCommand = ComicItemGridCommand<ComicNavigationItemGridViewModel, ComicNavigationItem>;
    using ComicPlaylistItemGridCommand = ComicItemGridCommand<ComicPlaylistItemGridViewModel, ComicNavigationItem>;

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

        public IAsyncOperation<ContentDialogResult> ShowConfirmRemoveItemDialogAsync()
            => this.ConfirmRemoveItemDialog.ShowAsync();

        #region Supporting classes

        // This class defined within ComicItemGrid to have access to VisibleComicsGrid
        public class CommandArgs<T, TItem> where T : ComicItemGridViewModel where TItem : ComicItem {
            public ComicItemGrid Grid { get; }
            public T ViewModel => (T)this.Grid.ViewModel;
            public MainViewModel MainViewModel => this.Grid.MainViewModel;
            public int Count => this.Grid.VisibleComicsGrid.SelectedItems.Count;
            public IEnumerable<TItem> Items => this.Grid.VisibleComicsGrid.SelectedItems.Cast<TItem>();
            public bool IsWorkItems => this.ViewModel.NavigationTag.IsWorkItemNavigationTag();
            public bool IsNavItems => !this.IsWorkItems;
            public int ComicCount => this.Items.SelectMany(i => i.ContainedComics()).Count();

            public CommandArgs(ComicItemGrid grid) {
                if (!(grid.ViewModel is T)) {
                    throw new ProgrammerError($"A wrong {nameof(CommandArgs<T, TItem>)} was created: incorrect view model type");
                }

                this.Grid = grid;
            }
        }

        #endregion

        #region Dynamic context menu items

        private void ComicItemGridContextFlyout_Opening(object sender, object e) {
            if (!(sender is MenuFlyout)) {
                throw new ProgrammerError("Only MenuFlyout should be allowed to call this handler");
            }

            // you can right click on empty space, but we don't want anything to happen
            if (this.VisibleComicsGrid.SelectedItems.Count == 0) {
                return;
            }

            // Update dynamic text when opening flyout
            _ = UpdateFlyoutItems(this.ComicItemGridContextFlyout.Items!);

            static bool UpdateFlyoutItems(IEnumerable<MenuFlyoutItemBase> flyoutItems) {
                var anyItemsEnabled = false;

                foreach (var item in flyoutItems) {
                    switch (item) {
                        case MenuFlyoutSubItem subitem:
                            subitem.Visibility = UpdateFlyoutItems(subitem.Items!)
                                ? Windows.UI.Xaml.Visibility.Visible
                                : Windows.UI.Xaml.Visibility.Collapsed;

                            continue;
                        case ComicsMenuFlyoutItem flyoutItem when flyoutItem.Command is IManuallyManagedCommand command: {
                            if (command.CanExecute()) {
                                flyoutItem.Text = command.GetName();
                                flyoutItem.Visibility = Windows.UI.Xaml.Visibility.Visible;
                                anyItemsEnabled = true;
                            } else {
                                flyoutItem.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                            }

                            break;
                        }
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
                if (!(grid.ViewModel is T)) {
                    return false;
                }

                var args = new ComicItemGrid.CommandArgs<T, TItem>(grid);
                if (args.Count == 0) {
                    return false;
                }

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

        public ComicPlaylistItemGridCommand CreatePlaylistCommand { get; }
        public ComicPlaylistItemGridCommand DeletePlaylistCommand { get; }

        private static string DescribeItem(string action, int count)
            => count == 1 ? action : $"{action} {count} items";

        internal ComicItemGridCommands(ComicItemGrid parent) {
            // Opens selected comics
            this.OpenItemsCommand = new ComicWorkItemGridCommand(parent,
                getName: e => DescribeItem("Open", e.ComicCount),
                execute: async e => await e.ViewModel.OpenItemsAsync(e.Items)
            );

            // Generates and executes a search limiting visible items to those selected
            this.SearchSelectedCommand = new ComicItemGridCommand(parent,
                name: "Search selected",
                execute: e => e.MainViewModel.FilterToSelected(e.Items),
                canExecute: e => e.IsNavItems || e.Count > 1
            );

            // Removes comics by asking the view model to do it for us
            this.RemoveItemCommand = new ComicItemGridCommand(parent,
                getName: e => DescribeItem("Remove", e.ComicCount),
                execute: async e => {
                    if (await e.Grid.ShowConfirmRemoveItemDialogAsync() != ContentDialogResult.Primary) {
                        return;
                    }

                    var comics = e.Items.SelectMany(item => item.ContainedComics()).ToList();
                    await e.MainViewModel.RemoveComicsAsync(comics);
                }
            );

            // Opens a flyout to move items between categories
            this.MoveFilesCommand = new ComicItemGridCommand(parent,
                getName: e => DescribeItem("Move", e.ComicCount),
                execute: async e => await e.Grid.ShowMoveFilesDialogAsync(e.Items));

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
                }
            );

            // Opens the containing folder in Windows Explorer
            this.GenerateThumbnailCommand = new ComicWorkItemGridCommand(parent,
                getName: e => e.Count == 1 ? "Generate thumbnail" : $"Generate thumbnails for {e.Count} items",
                execute: e => e.ViewModel.StartRequestGenerateThumbnailsTask(e.Items, replace: true));

            // Opens the comic info flyout to the "Edit Info" page
            this.RedefineThumbnailCommand = new ComicWorkItemGridCommand(parent,
                name: "Redefine thumbnail",
                execute: async e => await e.Grid.RedefineThumbnailAsync(e.Items.First()),
                canExecute: e => e.Count == 1
            );

            // Loves, or unloves comics
            this.LoveComicsCommand = new ComicWorkItemGridCommand(parent,
                getName: e => e.Items.All(i => i.IsLoved) ? DescribeItem("No longer love", e.Count) : DescribeItem("Love", e.Count),
                execute: async e => await e.ViewModel.ToggleLovedStatusForComicsAsync(e.Items));

            this.SearchAuthorCommand = new ComicWorkItemGridCommand(parent,
                getName: e => $"Show all items by {e.Items.First().Comic.Author}",
                execute: async e => {
                    var item = e.Items.First();
                    e.Grid.PrepareNavigateIn(item);

                    // TODO: the current program assumes that you cannot navigate into anything from a detail view.
                    // Much of the code is implemented with this assumption, causing navigating out of a detail view into 
                    // another detail view to crash the app. Therefore we have to navigate out first.
                    if (e.ViewModel.NavigationTag == NavigationTag.Detail) {
                        e.MainViewModel.NavigateOut();
                        await Task.Delay(150);
                    }

                    e.MainViewModel.FilterToAuthor(item.Comic.Author);
                },
                canExecute: e => e.Count == 1
            );


            // Navigates into the selected navigation item
            this.NavigateIntoCommand = new ComicNavigationItemGridCommand(parent,
                name: "Navigate into",
                execute: e => e.ViewModel.NavigateIntoItem(e.Items.First()),
                canExecute: e => e.Count == 1
            );

            // Creates a playlist
            this.CreatePlaylistCommand = new ComicPlaylistItemGridCommand(parent,
                name: $"Create playlist...",
                execute: async e => await e.Grid.ShowCreatePlaylistDialogAsync()
            );

            // Removes a playlist
            this.DeletePlaylistCommand = new ComicPlaylistItemGridCommand(parent,
                getName: e => $"Delete {e.Count.PluralString("playlist", simple: true)}",
                execute: async e => {
                    foreach (var item in e.Items) {
                        await e.MainViewModel.DeletePlaylistAsync(item.Title);
                    }
                }
            );

            // Popup dialog to add to playlist
            this.AddToPlaylistCommand = new ComicItemGridCommand(parent,
                name: "Add to playlist...",
                execute: async e => await e.Grid.ShowAddItemsToPlaylistDialogAsync(e.Items)
            );

            // Removes an item from the currently active playlist
            this.RemoveFromSelectedPlaylistCommand = new ComicWorkItemGridCommand(parent,
                getName: e => $"Remove from playlist '{e.ViewModel.Properties.PlaylistName}'", 
                execute: async e => await e.MainViewModel.RemoveFromPlaylistAsync(e.ViewModel.Properties.PlaylistName!, e.Items.Select(item => item.Comic)),
                canExecute: e => e.ViewModel.Properties.ParentType == NavigationTag.Playlist
            );

        }
    }
}
