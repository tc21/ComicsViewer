using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

using ComicItemGridCommand = ComicsViewer.Pages.ComicItemGridCommand<ComicsViewer.ViewModels.Pages.ComicItemGridViewModel>;
using ComicWorkItemGridCommand = ComicsViewer.Pages.ComicItemGridCommand<ComicsViewer.ViewModels.Pages.ComicWorkItemGridViewModel>;

#nullable enable

namespace ComicsViewer.Pages {

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

        // This class defined within ComicItemGrid to have access to VisibleComicsGrid
        public class CommandArgs<T> where T : ComicItemGridViewModel {
            public ComicItemGrid Grid { get; }
            public T ViewModel => (T)this.Grid.ViewModel!;
            public MainViewModel MainViewModel => this.Grid.MainViewModel!;
            public int Count => this.Grid.VisibleComicsGrid.SelectedItems.Count;
            public IEnumerable<ComicItem> Items => this.Grid.VisibleComicsGrid.SelectedItems.Cast<ComicItem>();
            public virtual bool IsWorkItems => this.ViewModel.NavigationTag.IsWorkItemNavigationTag();
            public virtual bool IsNavItems => !this.IsWorkItems;

            public CommandArgs(ComicItemGrid grid) {
                if (!(grid.ViewModel is T)) {
                    throw new ProgrammerError($"A wrong {nameof(CommandArgs<T>)} was created: incorrect view model type");
                }

                this.Grid = grid;
            }
        }
    }

    public class ComicItemGridCommand<T> : XamlUICommand where T : ComicItemGridViewModel {
        public ComicItemGridCommand(
            ComicItemGrid grid, 
            Action<ComicItemGrid.CommandArgs<T>> execute, 
            Func<ComicItemGrid.CommandArgs<T>, bool>? canExecute = null
        ) {
            this.CanExecuteRequested += (sender, args) => { 
                if (!(grid.ViewModel is T)) {
                    args.CanExecute = false;
                    return;
                }

                var ourArgs = new ComicItemGrid.CommandArgs<T>(grid);
                if (ourArgs.Count == 0) {
                    args.CanExecute = false;
                    return;
                }
                
                if (canExecute == null) {
                    args.CanExecute = true;
                } else {
                    args.CanExecute = canExecute(new ComicItemGrid.CommandArgs<T>(grid));
                }
            };

            this.ExecuteRequested += (sender, args) => execute(new ComicItemGrid.CommandArgs<T>(grid));
        }
    }

    public class ComicItemGridCommands {
        /* Since a StandardUICommand has an icon, but XamlUICommand doesn't, this is a good way to see which of 
         * our commands already have an icon and which ones need one defined in XAML */
        public ComicItemGridCommand OpenItemsCommand { get; }
        public ComicItemGridCommand SearchSelectedCommand { get; }
        public ComicItemGridCommand RemoveItemCommand { get; }
        public ComicItemGridCommand MoveFilesCommand { get; }

        public ComicWorkItemGridCommand ShowInExplorerCommand { get; }
        public ComicWorkItemGridCommand GenerateThumbnailCommand { get; }
        public ComicWorkItemGridCommand EditInfoCommand { get; }
        public ComicWorkItemGridCommand RedefineThumbnailCommand { get; }
        public ComicWorkItemGridCommand LoveComicsCommand { get; }
        public ComicWorkItemGridCommand DislikeComicsCommand { get; }
        public ComicWorkItemGridCommand SearchAuthorCommand { get; }

        // note: we don't have a NavItemGridCommand yet because NavItemViewModel doesn't provide any unique behavior
        public ComicItemGridCommand EditNavigationItemCommand { get; }

        //private readonly ComicItemGrid parent;
        //private int SelectedItemCount => parent.VisibleComicsGrid.SelectedItems.Count;
        //private IEnumerable<ComicItem> SelectedItems => parent.VisibleComicsGrid.SelectedItems.Cast<ComicItem>();
        //// 1. assuming all of the same type; 2. if count = 0 then it doesn't matter
        //private ComicItemType SelectedItemType => this.SelectedItems.Count() == 0 ? ComicItemType.Work : this.SelectedItems.First().ItemType;

        internal ComicItemGridCommands(ComicItemGrid parent) {
            // Opens selected comics or navigates into the selected navigation item
            this.OpenItemsCommand = new ComicItemGridCommand(parent,
                execute: async args => await args.ViewModel.OpenItemsAsync(args.Items),
                canExecute: args => args.IsWorkItems || args.Count == 1
            );

            // Generates and executes a search limiting visible items to those selected
            this.SearchSelectedCommand = new ComicItemGridCommand(parent,
                execute: args => args.MainViewModel.FilterToSelected(args.Items),
                canExecute: args => args.IsNavItems || args.Count > 1
            );

            // Removes comics by asking the view model to do it for us
            this.RemoveItemCommand = new ComicItemGridCommand(parent,
                execute: async args => {
                    if ((await args.Grid.ShowConfirmRemoveItemDialogAsync()) == ContentDialogResult.Primary) {
                        var comics = args.Items.SelectMany(item => item.Comics).ToList();
                        await args.MainViewModel.RemoveComicsAsync(comics);
                    }
                }
            );

            // Opens a flyout to move items between categories
            this.MoveFilesCommand = new ComicItemGridCommand(parent,
                execute: async args => await args.Grid.ShowMoveFilesDialogAsync(args.Items));


            // Opens the containing folder in Windows Explorer
            this.ShowInExplorerCommand = new ComicWorkItemGridCommand(parent,
                execute: args => {
                    foreach (var item in args.Items) {
                        _ = Startup.OpenContainingFolderAsync(item.TitleComic);
                    }
                }
            );

            // Opens the containing folder in Windows Explorer
            this.GenerateThumbnailCommand = new ComicWorkItemGridCommand(parent,
                execute: args => args.ViewModel.RequestGenerateThumbnails(args.Items, replace: true));

            // Opens the comic info flyout to the "Edit Info" page
            this.EditInfoCommand = new ComicWorkItemGridCommand(parent,
                execute: async args => await args.Grid.ShowEditComicInfoDialogAsync(args.Items.First()),
                canExecute: args => args.Count == 1
            );

            // Opens the comic info flyout to the "Edit Info" page
            this.RedefineThumbnailCommand = new ComicWorkItemGridCommand(parent,
                execute: async args => await args.Grid.RedefineThumbnailAsync(args.Items.First()),
                canExecute: args => args.Count == 1
            );

            // Loves, or unloves comics
            this.LoveComicsCommand = new ComicWorkItemGridCommand(parent,
                execute: async args => await args.ViewModel.ToggleLovedStatusForComicsAsync(args.Items));

            this.DislikeComicsCommand = new ComicWorkItemGridCommand(parent,
                execute: async args => await args.ViewModel.ToggleDislikedStatusForComicsAsync(args.Items));

            this.SearchAuthorCommand = new ComicWorkItemGridCommand(parent, 
                execute: args => args.MainViewModel.FilterToAuthor(args.Items.First().TitleComic.DisplayAuthor),
                canExecute: args => args.Count == 1
            );


            // Renames a tag, etc.
            this.EditNavigationItemCommand = new ComicItemGridCommand(parent,
                execute: async args => await args.Grid.ShowEditNavigationItemDialogAsync(args.Items.First()),
                // TODO implement editing for authors and categories as well
                canExecute: args => args.ViewModel.NavigationTag == Support.NavigationTag.Tags && args.Count == 1
            );
        }
    }
}
