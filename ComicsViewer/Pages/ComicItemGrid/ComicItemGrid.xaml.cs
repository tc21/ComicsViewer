using ComicsLibrary;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public sealed partial class ComicItemGrid : Page {
        public MainViewModel? MainViewModel => ViewModel?.MainViewModel;
        public ComicItemGridViewModel? ViewModel;

        public ComicItemGrid() {
            this.InitializeComponent();
            this.ContextMenuCommands = new ComicItemGridCommands(this);

            Debug.WriteLine($"{debug_this_count} created");

            // This has to be done in code, not XAML since we need the third handledEventsToo argument
            this.VisibleComicsGrid.AddHandler(
                PointerPressedEvent, new PointerEventHandler(this.VisibleComicsGrid_PointerPressed), true);
        }

        ~ComicItemGrid() {
            Debug.WriteLine($"{debug_this_count} destroyed");
        }

        // This function determines if the user used their non-left-mouse-button to click, and prevents the Tapped
        // evenet from firing if that is the case.
        private void VisibleComicsGrid_PointerPressed(object sender, PointerRoutedEventArgs e) {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse) {
                var pointerPoint = e.GetCurrentPoint(this);
                if (!pointerPoint.Properties.IsLeftButtonPressed) {
                    this.isLastTapValid = false;
                    return;
                }
            }

            this.isLastTapValid = true;
        }

        private bool isLastTapValid = true;

        private async void VisibleComicsGrid_Tapped(object sender, TappedRoutedEventArgs e) {
            if (!this.isLastTapValid) {
                return;
            }

            var controlKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            var shiftKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift);

            if ((controlKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                (shiftKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down) {
                // The user is selecting something. Ignore this.
                return;
            }

            if (!(sender is GridView)) {
                throw new ApplicationLogicException("Only ComicItemGrid should be able to call this event handler");
            }

            var tappedElement = (FrameworkElement)e.OriginalSource;
            if (!(tappedElement.DataContext is ComicItem comicItem)) {
                // The click happened on an empty space
                this.VisibleComicsGrid.SelectedItems.Clear();
                return;
            }

            if (comicItem.ItemType == ComicItemType.Navigation) {
                await this.ViewModel!.OpenItemsAsync(new[] { comicItem });
                return;
            }

            this.ComicInfoFlyout.OverlayInputPassThroughElement = this.ContainerGrid;
            this.ComicInfoFlyout.NavigateAndShowAt(
                typeof(ComicInfoFlyoutContent), 
                new ComicInfoFlyoutNavigationArguments(this.ViewModel!, comicItem, 
                        async () => await this.ShowEditComicInfoDialogAsync(comicItem)),
                tappedElement);
        }

        private async void VisibleComicsGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) {
            var controlKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            var shiftKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift);

            if ((controlKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                (shiftKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down) {
                // Ctrl/Shift+right clicks are ignored, just like in SingleTapped
                // The user is selecting something. Ignore this.
                return;
            }

            var tappedElement = (FrameworkElement)e.OriginalSource;
            if (!(tappedElement.DataContext is ComicItem comicItem)) {
                // The click happened on an empty space
                this.VisibleComicsGrid.SelectedItems.Clear();
                return;
            }

            this.ComicInfoFlyout.Hide();
            await this.ViewModel!.OpenItemsAsync(new[] { comicItem });
        }

        public async Task ShowEditComicInfoDialogAsync(ComicItem item) {
            // Since the only preset UI is its title, there's no need to have this in the xaml. We can just create it here.
            _ = await new PagedContentDialog { Title = "Edit info" }.NavigateAndShowAsync(
                typeof(EditComicInfoDialogContent), 
                new EditComicInfoDialogNavigationArguments(this.ViewModel!, item)
            );
        }

        public async Task ShowMoveFilesDialogAsync(IEnumerable<ComicItem> items) {
            /* Currently, the application is not able to handle moving files while changing its author or title. So the
             * only thing we can actually change is category. We are thus limiting the ability to move files to moving
             * between the already-defined categories. */
            _ = await new PagedContentDialog { Title = "Move files to a new category" }.NavigateAndShowAsync(
                typeof(MoveFilesDialogContent),
                new MoveFilesDialogNavigationArguments(this.ViewModel!, items.SelectMany(item => item.Comics).ToList())
            );
        }

        // Prepares the grid before the right click context menu is shown
        private void VisibleComicsGrid_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            if (!(sender is GridView grid)) {
                throw new ApplicationLogicException("Only ComicItemGrid should be able to call this event handler");
            }

            var tappedElement = (FrameworkElement)e.OriginalSource;

            if (!(tappedElement.DataContext is ComicItem comicItem)) {
                this.VisibleComicsGrid.SelectedItems.Clear();
                // The right click happened on an empty space
                return;
            }

            // Selection logic that behaves like most other reasonable apps
            if (!grid.SelectedItems.Contains(comicItem)) {
                grid.SelectedItems.Clear();
                grid.SelectedItems.Add(comicItem);
            }

            this.ComicItemGridContextFlyout.ShowAt(tappedElement, new FlyoutShowOptions {
                Position = e.GetPosition(tappedElement)
            });
        }

        #region Controlling from MainPage

        internal void ScrollToTop() {
            if (this.ViewModel != null && this.ViewModel.ComicItems.Count > 0) {
                // Looks like there isn't a good way to actually scroll with an animation. 
                // Both GridView.ScrollIntoView and GridView.MakeVisible scrolls instantly without animation.
                // The solution used here is a reverse-engineering that can potentially break anytime with an update to the UWP library
                if (VisualTreeHelper.GetChild(this.VisibleComicsGrid, 0) == null) {
                    // this means the grid hasn't even loaded yet, just ignore the request
                    return;
                }

                if (VisualTreeHelper.GetChild(this.VisibleComicsGrid, 0) is UIElement childElement &&
                    VisualTreeHelper.GetChild(childElement, 0) is ScrollViewer scrollViewer) {
                    scrollViewer.ChangeView(null, -scrollViewer.VerticalOffset, null);
                } else {
                    throw new ApplicationLogicException("Failed to obtain VisibleComicsGrid's ScrollViewer child element.");
                }
            }
        }

        #endregion

        #region Saving and setting state during navigation

        private static int debug_count = 0;
        private readonly int debug_this_count = ++debug_count;

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is ComicItemGridNavigationArguments args)) {
                throw new ApplicationLogicException("A ComicItemGrid must receive a ComicItemGridNavigationArguments as its parameter.");
            }

            if (args.ViewModel == null) {
                throw new ApplicationLogicException();
            }

            if (e.NavigationMode == NavigationMode.New) {
                // Initialize this page only when creating a new page, 
                // not when the user returned to this page by pressing the back button
                this.ViewModel = args.ViewModel;
                Debug.WriteLine($"{debug_this_count} OnNavigatedTo (new)");
            } else {
                Debug.WriteLine($"{debug_this_count} OnNavigatedTo ({e.NavigationMode})");
                // ?
            }

            this.ViewModel!.IsVisibleViewModel = true;
            if (this.VisibleComicsGrid.IsLoaded) {
                this.RecalculateGridItemSize(this.VisibleComicsGrid);
            }

            CoreWindow.GetForCurrentThread().ResizeCompleted += this.ComicItemGrid_ResizeCompleted;

            // MainPage cannot rely on ContentFrame.Navigated because we navigate to a ComicItemGridContainer, not this class
            args.OnNavigatedTo?.Invoke(this, e);
        }

        internal void ManuallyNavigatedTo(NavigationEventArgs e) {
            this.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
            Debug.WriteLine($"{debug_this_count} OnNavigatingFrom");
            this.ViewModel!.IsVisibleViewModel = false;
            CoreWindow.GetForCurrentThread().ResizeCompleted -= this.ComicItemGrid_ResizeCompleted;
        }

        #endregion

        #region Redefining thumbnails

        private async Task RedefineThumbnailAsync(ComicItem item) {
            var folder = await StorageFolder.GetFolderFromPathAsync(item.TitleComic.Path);
            var images = await Thumbnail.GetPossibleThumbnailFilesAsync(folder);

            _ = await new PagedContentDialog { Title = "Redefine thumbnail" }.NavigateAndShowAsync(
                typeof(RedefineThumbnailDialogContent),
                new RedefineThumbnailDialogNavigationArguments(images, item, this.ViewModel!)
            );
        }

        #endregion

        #region Context menu commands

        public ComicItemGridCommands ContextMenuCommands { get; }

        /* A note on keyboard shortcuts: KeyboardAccelerators seem to only run when the control responsible for the 
         * command is available. This translates to shortcuts only working then the command's CanExecute evaluated to 
         * true the last time the context menu flyout was shown. Since we can't get commands to consistently execute 
         * without rewriting the system, they are disabled for now. */

        public class ComicItemGridCommands {
            /* Since a StandardUICommand has an icon, but XamlUICommand doesn't, this is a good way to see which of 
             * our commands already have an icon and which ones need one defined in XAML */
            public StandardUICommand OpenItemsCommand { get; }
            public XamlUICommand SearchSelectedCommand { get; }
            public StandardUICommand RemoveItemCommand { get; }
            public XamlUICommand ShowInExplorerCommand { get; }
            public XamlUICommand GenerateThumbnailCommand { get; }
            public XamlUICommand EditInfoCommand { get; }
            public XamlUICommand RedefineThumbnailCommand { get; }
            public XamlUICommand LoveComicsCommand { get; }
            public XamlUICommand DislikeComicsCommand { get; }
            public XamlUICommand MoveFilesCommand { get; }

            private readonly ComicItemGrid parent;
            private int SelectedItemCount => parent.VisibleComicsGrid.SelectedItems.Count;
            private IEnumerable<ComicItem> SelectedItems => parent.VisibleComicsGrid.SelectedItems.Cast<ComicItem>();
            // 1. assuming all of the same type; 2. if count = 0 then it doesn't matter
            private ComicItemType SelectedItemType => this.SelectedItems.Count() == 0 ? ComicItemType.Work : this.SelectedItems.First().ItemType;

            internal ComicItemGridCommands(ComicItemGrid parent) {
                this.parent = parent;

                // Opens selected comics or navigates into the selected navigation item
                this.OpenItemsCommand = new StandardUICommand(StandardUICommandKind.Open);
                this.OpenItemsCommand.ExecuteRequested += async (sender, args) => await parent.ViewModel!.OpenItemsAsync(this.SelectedItems);
                this.OpenItemsCommand.CanExecuteRequested += this.CanExecuteHandler(()
                    => this.SelectedItemType == ComicItemType.Work || this.SelectedItemCount == 1);

                // Generates and executes a search limiting visible items to those selected
                this.SearchSelectedCommand = new XamlUICommand();
                this.SearchSelectedCommand.ExecuteRequested += (sender, args) => parent.MainViewModel!.NavigateIntoSelected(this.SelectedItems);
                this.SearchSelectedCommand.CanExecuteRequested += this.CanExecuteHandler(()
                    => this.SelectedItemType == ComicItemType.Navigation || this.SelectedItemCount > 1);

                // Removes comics by asking the view model to do it for us
                this.RemoveItemCommand = new StandardUICommand(StandardUICommandKind.Delete);
                this.RemoveItemCommand.ExecuteRequested += async (sender, args) => {
                    if ((await parent.ConfirmRemoveItemDialog.ShowAsync()) == ContentDialogResult.Primary) {
                        var comics = this.SelectedItems.SelectMany(item => item.Comics).ToList();
                        await parent.MainViewModel!.RemoveComicsAsync(comics);
                    }
                };

                // Opens the containing folder in Windows Explorer
                this.ShowInExplorerCommand = new XamlUICommand();
                this.ShowInExplorerCommand.ExecuteRequested += (sender, args) => {
                    foreach (var item in this.SelectedItems) {
                        Startup.OpenContainingFolderAsync(item.TitleComic);
                    }
                };
                this.ShowInExplorerCommand.CanExecuteRequested += this.CanExecuteHandler(()
                    => this.SelectedItemType == ComicItemType.Work);

                // Opens the containing folder in Windows Explorer
                this.GenerateThumbnailCommand = new XamlUICommand();
                this.GenerateThumbnailCommand.ExecuteRequested += (sender, args)
                    => parent.ViewModel!.RequestGenerateThumbnails(this.SelectedItems, replace: true);
                this.GenerateThumbnailCommand.CanExecuteRequested += this.CanExecuteHandler(()
                    => this.SelectedItemType == ComicItemType.Work);

                // Opens the comic info flyout to the "Edit Info" page
                this.EditInfoCommand = new XamlUICommand();
                this.EditInfoCommand.ExecuteRequested += async (sender, args)
                    // We don't know the actual grid item element, so we just show the flyout at the center of the screen
                    => await parent.ShowEditComicInfoDialogAsync(this.SelectedItems.First());
                this.EditInfoCommand.CanExecuteRequested += this.CanExecuteHandler(()
                    => this.SelectedItemType == ComicItemType.Work && this.SelectedItemCount == 1);

                // Opens the comic info flyout to the "Edit Info" page
                this.RedefineThumbnailCommand = new XamlUICommand();
                this.RedefineThumbnailCommand.ExecuteRequested += async (sender, args)
                    // We don't know the actual grid item element, so we just show the flyout at the center of the screen
                    => await parent.RedefineThumbnailAsync(this.SelectedItems.First());
                this.RedefineThumbnailCommand.CanExecuteRequested += this.CanExecuteHandler(()
                    => this.SelectedItemType == ComicItemType.Work && this.SelectedItemCount == 1);

                // Loves, or unloves comics
                this.LoveComicsCommand = new XamlUICommand();
                this.LoveComicsCommand.ExecuteRequested += async (sender, args)
                    // We don't know the actual grid item element, so we just show the flyout at the center of the screen
                    => await parent.ViewModel!.ToggleLovedStatusForComicsAsync(this.SelectedItems);
                this.LoveComicsCommand.CanExecuteRequested += this.CanExecuteHandler(()
                    => this.SelectedItemType == ComicItemType.Work);

                // Loves, or unloves comics
                this.DislikeComicsCommand = new XamlUICommand();
                this.DislikeComicsCommand.ExecuteRequested += async (sender, args)
                    // We don't know the actual grid item element, so we just show the flyout at the center of the screen
                    => await parent.ViewModel!.ToggleDislikedStatusForComicsAsync(this.SelectedItems);
                this.DislikeComicsCommand.CanExecuteRequested += this.CanExecuteHandler(()
                    => this.SelectedItemType == ComicItemType.Work);

                // Opens a flyout to move items between categories
                this.MoveFilesCommand = new XamlUICommand();
                this.MoveFilesCommand.ExecuteRequested += async (sender, args)
                    // We don't know the actual grid item element, so we just show the flyout at the center of the screen
                    => await parent.ShowMoveFilesDialogAsync(this.SelectedItems);
            }

            private TypedEventHandler<XamlUICommand, CanExecuteRequestedEventArgs> CanExecuteHandler(Func<bool> predicate) {
                return (sender, args) => args.CanExecute = predicate();
            }
        }

        #endregion

        #region Dynamic context menu items

        private void ComicItemGridContextFlyout_Opening(object sender, object e) {
            if (!(sender is MenuFlyout)) {
                throw new ApplicationLogicException("Only MenuFlyout should be allowed to call this handler");
            }

            // you can right click on empty space, but we don't want anything to happen
            if (this.VisibleComicsGrid.SelectedItems.Count == 0) {
                return;
            }

            // Update dynamic text when opening flyout
            UpdateFlyoutItemsText(this.ComicItemGridContextFlyout.Items);

            void UpdateFlyoutItemsText(IEnumerable<MenuFlyoutItemBase> flyoutItems) {
                foreach (var item in flyoutItems) {
                    if (item is MenuFlyoutSubItem subitem) {
                        UpdateFlyoutItemsText(subitem.Items);
                        continue;
                    }

                    if (item.Tag != null && item is MenuFlyoutItem flyoutItem) {
                        flyoutItem.Text = this.GetDynamicFlyoutText(item.Tag.ToString());
                    }
                }
            }
        }

        private string GetDynamicFlyoutText(string tag) {
            var type = ((ComicItem)this.VisibleComicsGrid.SelectedItems[0]).ItemType;
            var count = this.VisibleComicsGrid.SelectedItems.Count;
            IEnumerable<ComicItem> ComicItems() => this.VisibleComicsGrid.SelectedItems.Cast<ComicItem>();
            int ComicsCount() => this.VisibleComicsGrid.SelectedItems.Cast<ComicItem>().SelectMany(i => i.Comics).Count();

            return tag switch {
                "open" => (type == ComicItemType.Work ? "Open" : "Navigate into") + (count > 1 ? $" {count} items" : ""),
                "search" => "Search selected",
                "move" => ComicsCount() == 1 ? "Move comic" : $"Move {ComicsCount().PluralString("comic")}",
                // why exactly can't we use blocks in a switch expression?
                "remove" => (type == ComicItemType.Work && count == 1) ? "Remove" : 
                            $"Remove {ComicsCount().PluralString("comic")}",
                "showInExplorer" => count == 1 ? "Show in Explorer" : $"Show {count} items in Explorer",
                "generateThumbnail" => count == 1 ? "Generate thumbnail" : $"Generate thumbnails for {count} items",
                "love" => (ComicItems().All(item => item.IsLoved) ? "No longer love" : "Love") +
                          (count == 1 ? "" : " " + count.PluralString("comic")),
                "dislike" => (ComicItems().All(item => item.IsDisliked) ? "No longer dislike" : "Dislike") +
                             (count == 1 ? "" : " " + count.PluralString("comic")),
                _ => throw new ApplicationLogicException($"Unhandled tag name for flyout item: '{tag}'")
            };
        }

        #endregion

        #region Dynamic resizing

        /* We will have to manage item widths manually. Note that the Windows Community Toolkit provides the ability ]
         * to do this without writing custom code using AdaptiveGridView, but that uses a min width for each item, 
         * when we want a max width for each item. */
        private void ComicItemGrid_ResizeCompleted(CoreWindow sender, object args) {
            this.RecalculateGridItemSize(this.VisibleComicsGrid);
        }

        private void RecalculateGridItemSize(GridView grid) {
            if (this.ViewModel == null) {
                return;
            }

            var idealItemWidth = this.ViewModel.ImageWidth;
            var idealItemHeight = this.ViewModel.ImageHeight;
            var columns = Math.Ceiling(grid.ActualWidth / idealItemWidth);
            var itemsWrapGrid = (ItemsWrapGrid)grid.ItemsPanelRoot;
            itemsWrapGrid.ItemWidth = grid.ActualWidth / columns;
            itemsWrapGrid.ItemHeight = itemsWrapGrid.ItemWidth * idealItemHeight / idealItemWidth;
        }

        #endregion

        #region Drag and drop

        private void VisibleComicsGrid_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            Debug.Write("DragItemsCompleted");
        }

        private void VisibleComicsGrid_DragItemsStarting(object sender, DragItemsStartingEventArgs e) {
            var comicItems = e.Items.Cast<ComicItem>().ToList();

            e.Data.RequestedOperation = DataPackageOperation.Copy;

            e.Data.SetDataProvider(StandardDataFormats.StorageItems, async request => {
                var items = new List<IStorageItem>();

                // Required to call async functions
                var deferral = request.GetDeferral();

                foreach (var comicItem in comicItems) {
                    foreach (var comic in comicItem.Comics) {
                        items.Add(await StorageFolder.GetFolderFromPathAsync(comic.Path));
                    }
                }

                request.SetData(items.AsReadOnly());

                deferral.Complete();
            });

            Debug.Write("DragItemsStarting");
        }

        private void VisibleComicsGrid_DragOver(object sender, DragEventArgs e) {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void VisibleComicsGrid_Drop(object sender, DragEventArgs e) {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) {
                return;
            }

            var items = await e.DataView.GetStorageItemsAsync();

            if (items.Count == 0) {
                return;
            }

            var folders = items.Where(item => item.IsOfType(StorageItemTypes.Folder))
                               .Cast<StorageFolder>();

            await this.MainViewModel!.RequestLoadComicsFromFoldersAsync(folders);
        }

        #endregion

        private void VisibleComicsGrid_Loaded(object sender, RoutedEventArgs e) {
            this.RecalculateGridItemSize(this.VisibleComicsGrid);
        }
    }
}