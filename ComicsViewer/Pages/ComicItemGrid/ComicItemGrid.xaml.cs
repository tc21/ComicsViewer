using ComicsViewer.ClassExtensions;
using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using WinRTXamlToolkit.Controls.Extensions;

#nullable enable

namespace ComicsViewer.Pages {
    public sealed partial class ComicItemGrid : Page {
        public MainViewModel? MainViewModel => ViewModel?.MainViewModel;
        public ComicItemGridViewModel? ViewModel;

        private ScrollViewer? VisibleComicsGridScrollViewer;

        public ComicItemGrid() {
            this.InitializeComponent();

            Debug.WriteLine($"{debug_this_count} created");

            // This has to be done in code, not XAML since we need the third handledEventsToo argument
            this.VisibleComicsGrid.AddHandler(
                PointerPressedEvent, new PointerEventHandler(this.VisibleComicsGrid_PointerPressed), true);
        }

        ~ComicItemGrid() {
            Debug.WriteLine($"{debug_this_count} destroyed");
        }

        #region Clicking, double clicking, right clicking

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

        private void VisibleComicsGrid_Tapped(object sender, TappedRoutedEventArgs e) {
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
                throw new ProgrammerError("Only ComicItemGrid should be able to call this event handler");
            }

            var tappedElement = (FrameworkElement)e.OriginalSource;
            if (!(tappedElement.DataContext is ComicItem item)) {
                // The click happened on an empty space
                this.VisibleComicsGrid.SelectedItems.Clear();
                return;
            }

            switch (this.ViewModel) {  // switch ComicItemGridViewModel
                case ComicWorkItemGridViewModel vm:
                    var workItem = (ComicWorkItem)item;
                    this.ComicInfoFlyout.OverlayInputPassThroughElement = this.ContainerGrid;
                    this.ComicInfoFlyout.NavigateAndShowAt(
                        typeof(ComicInfoFlyoutContent),
                        new ComicInfoFlyoutNavigationArguments(this.ViewModel!, workItem,
                                async () => await this.ShowEditComicInfoDialogAsync(workItem)),
                        tappedElement);
                    return;

                case ComicNavigationItemGridViewModel vm:
                    vm.NavigateIntoItem((ComicNavigationItem)item);
                    return;

                default:
                    throw new ProgrammerError("unhandled switch case");
            } 
        }

        private async void VisibleComicsGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) {
            if (!(this.ViewModel is ComicWorkItemGridViewModel vm)) {
                return;
            }

            var controlKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            var shiftKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift);

            if ((controlKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                (shiftKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down) {
                // Ctrl/Shift+right clicks are ignored, just like in SingleTapped
                // The user is selecting something. Ignore this.
                return;
            }

            var tappedElement = (FrameworkElement)e.OriginalSource;
            if (!(tappedElement.DataContext is ComicWorkItem item)) {
                // The click happened on an empty space
                this.VisibleComicsGrid.SelectedItems.Clear();
                return;
            }

            this.ComicInfoFlyout.Hide();
            await vm.OpenItemsAsync(new[] { item });
        }

        // Prepares the grid before the right click context menu is shown
        private void VisibleComicsGrid_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            if (!(sender is GridView grid)) {
                throw new ProgrammerError("Only ComicItemGrid should be able to call this event handler");
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

        #endregion

        #region Popups

        public async Task ShowEditComicInfoDialogAsync(ComicWorkItem item) {
            // Since the only preset UI is its title, there's no need to have this in the xaml. We can just create it here.
            _ = await new PagedContentDialog { Title = "Edit info" }.NavigateAndShowAsync(
                typeof(EditComicInfoDialogContent),
                new EditComicInfoDialogNavigationArguments(this.ViewModel!, item)
            );
        }

        public async Task ShowEditNavigationItemDialogAsync(ComicNavigationItem item) {
            if (!(this.ViewModel is ComicNavigationItemGridViewModel vm)) {
                throw new ProgrammerError($"{nameof(ShowEditNavigationItemDialogAsync)} should not be called with a work item view model");
            }

            _ = await new PagedContentDialog { Title = $"{vm.NavigationTag.Describe(capitalized: true)}: {item.Title}" }.NavigateAndShowAsync(
                typeof(EditNavigationItemDialogContent),
                new EditNavigationItemDialogNavigationArguments(vm, item.Title)
            );
        }

        public async Task ShowMoveFilesDialogAsync(IEnumerable<ComicItem> items) {
            /* Currently, the application is not able to handle moving files while changing its author or title. So the
             * only thing we can actually change is category. We are thus limiting the ability to move files to moving
             * between the already-defined categories. */
            _ = await new PagedContentDialog { Title = "Move files to a new category" }.NavigateAndShowAsync(
                typeof(MoveFilesDialogContent),
                new MoveFilesDialogNavigationArguments(this.ViewModel!, items.SelectMany(item => item.ContainedComics()).ToList())
            );
        }

        #endregion

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
                    _ = scrollViewer.ChangeView(null, -scrollViewer.VerticalOffset, null);
                } else {
                    throw new ProgrammerError("Failed to obtain VisibleComicsGrid's ScrollViewer child element.");
                }
            }
        }

        #endregion

        #region Saving and setting state during navigation

        private static int debug_count = 0;
        private readonly int debug_this_count = ++debug_count;

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is ComicItemGridNavigationArguments args)) {
                throw new ProgrammerError("A ComicItemGrid must receive a ComicItemGridNavigationArguments as its parameter.");
            }

            if (args.ViewModel == null) {
                throw new ProgrammerError("A ComicItemGrid must received a viewmodel in its navigation arguments");
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

            if (this.VisibleComicsGrid.IsLoaded) {
                this.RecalculateGridItemSize(this.VisibleComicsGrid);
            }

            CoreWindow.GetForCurrentThread().ResizeStarted += this.ComicItemGrid_ResizeStarted;
            CoreWindow.GetForCurrentThread().ResizeCompleted += this.ComicItemGrid_ResizeCompleted;

            // MainPage cannot rely on ContentFrame.Navigated because we navigate to a ComicItemGridContainer, not this class
            args.OnNavigatedTo?.Invoke(this, e);
        }

        internal void ManuallyNavigatedTo(NavigationEventArgs e) {
            this.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
            // Under the current implementation, this page is never used again after a call to OnNavigatingFrom.
            Debug.WriteLine($"{debug_this_count} OnNavigatingFrom");
            this.ViewModel!.Dispose();
            CoreWindow.GetForCurrentThread().ResizeStarted -= this.ComicItemGrid_ResizeStarted;
            CoreWindow.GetForCurrentThread().ResizeCompleted -= this.ComicItemGrid_ResizeCompleted;
        }

        #endregion

        #region Redefining thumbnails

        public async Task RedefineThumbnailAsync(ComicWorkItem item) {
            if (!(await item.Comic.GetFolderAndNotifyErrorsAsync() is StorageFolder folder)) {
                return;
            }

            var images = await Thumbnail.GetPossibleThumbnailFilesAsync(folder);

            _ = await new PagedContentDialog { Title = "Redefine thumbnail" }.NavigateAndShowAsync(
                typeof(RedefineThumbnailDialogContent),
                new RedefineThumbnailDialogNavigationArguments(images, item, this.ViewModel!)
            );
        }

        #endregion

        #region Dynamic resizing

        /* We will have to manage item widths manually. Note that the Windows Community Toolkit provides the ability ]
         * to do this without writing custom code using AdaptiveGridView, but that uses a min width for each item, 
         * when we want a max width for each item. */
        private bool resizing = false;

        private void ComicItemGrid_ResizeStarted(CoreWindow sender, object args) {
            this.resizing = true;
        }


        private void ComicItemGrid_ResizeCompleted(CoreWindow sender, object args) {
            this.resizing = false;
            this.RecalculateGridItemSize(this.VisibleComicsGrid);
        }

        private void VisibleComicsGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (!this.resizing) {
                this.RecalculateGridItemSize(this.VisibleComicsGrid);
            }
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
            e.Data.Properties.Add("tc.comics.send_items", true);
            e.Data.SetDataProvider(StandardDataFormats.StorageItems, async request => {
                var items = new List<IStorageItem>();

                // Required to call async functions
                var deferral = request.GetDeferral();

                foreach (var comicItem in comicItems) {
                    foreach (var comic in comicItem.ContainedComics()) {
                        items.Add(await StorageFolder.GetFolderFromPathAsync(comic.Path));
                    }
                }

                request.SetData(items.AsReadOnly());

                deferral.Complete();
            });

            Debug.Write("DragItemsStarting");
        }

        private void VisibleComicsGrid_DragOver(object sender, DragEventArgs e) {
            if (e.Data != null && e.Data.Properties.ContainsKey("tc.comics.send_items")) {
                return;
            }

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

            await this.MainViewModel!.StartLoadComicsFromFoldersTaskAsync(folders);
        }

        #endregion

        private void VisibleComicsGrid_Loaded(object sender, RoutedEventArgs e) {
            // We have to access the scrollviewer programatically
            this.VisibleComicsGridScrollViewer = this.VisibleComicsGrid.GetFirstDescendantOfType<ScrollViewer>();
            if (this.VisibleComicsGridScrollViewer == null) {
                throw new ProgrammerError("Could not retrieve VisibleComicsGrid ScrollViewer");
            }

            this.VisibleComicsGridScrollViewer.ViewChanged += this.VisibleComicsGridScrollViewer_ViewChanged;

            this.RecalculateGridItemSize(this.VisibleComicsGrid);
        }

        private void VisibleComicsGridScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) {
            if (this.VisibleComicsGridScrollViewer == null) {
                // impossible
                return;
            }

            // we arbitrarily say 2000 pixels is close enough to the bottom.
            if (this.VisibleComicsGridScrollViewer.ScrollableHeight - this.VisibleComicsGridScrollViewer.VerticalOffset < 2000) {
                this.ViewModel?.RequestComicItems();
            }
        }
    }
}