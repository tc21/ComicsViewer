using ComicsLibrary;
using ComicsViewer.Pages;
using ComicsViewer.Profiles;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer {
    public sealed partial class ComicItemGrid : Page {
        public MainViewModel? MainViewModel => ViewModel?.MainViewModel;
        public ComicItemGridViewModel? ViewModel;

        public ComicItemGrid() {
            this.InitializeComponent();
            this.ContextMenuCommands = new ComicItemGridCommands(this);
        }

        private async void VisibleComicsGrid_Tapped(object sender, TappedRoutedEventArgs e) {
            var controlKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            var shiftKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift);

            if ((controlKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                (shiftKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down) {
                // The user is selecting something. Ignore this.
                return;
            }

            // TODO we need to fix this triggering for the mouse forward/backwards buttons

            if (!(sender is GridView)) {
                throw new ApplicationLogicException("Only ComicItemGrid should be able to call this event handler");
            }

            var tappedElement = (FrameworkElement)e.OriginalSource;
            if (!(tappedElement.DataContext is ComicItem comicItem)) {
                // The right click happened on an empty space
                return;
            }

            if (comicItem.ItemType == ComicItemType.Navigation) {
                await this.ViewModel!.OpenItemsAsync(new[] { comicItem });
                return;
            }

            // When this is implemented, it will completely replace the DoubleTapped features. 
            var flyout = (this.Resources["ComicInfoFlyout"] as Flyout)!;
            this.ComicInfoFlyoutFrame.Navigate(typeof(ComicInfoPage), 
                new ComicInfoPageNavigationArguments(this.ViewModel!, comicItem));
            flyout.ShowAt(tappedElement, new FlyoutShowOptions { ExclusionRect = new Rect(0, 0, 0, 0) });

            // This is a hack to enable double-tap opening: If the user clicks twice in a row, the second click
            // dismisses the flyout, so we only end up capturing a PointerReleased event
            // TODO: currently if the user clicks an item but then clicks again outside of the item, it is still
            // interpreted as a double-click. We should detect and ignore those kinds of situations.
            this.doubleTapPointerReleased = false;
            this.singleTapPosition = e.GetPosition(this.VisibleComicsGrid);

            await Task.Delay(500);

            if (this.doubleTapPointerReleased == true) {
                flyout.Hide();
                await this.ViewModel!.OpenItemsAsync(new[] { comicItem });
            }

            this.singleTapPosition = null;

        }

        private Point? singleTapPosition;
        private bool doubleTapPointerReleased;

        private void VisibleComicsGrid_PointerReleased(object sender, PointerRoutedEventArgs e) {
            if (!(this.singleTapPosition is Point lastPosition)) {
                return;
            }

            var tapPosition = e.GetCurrentPoint(this.VisibleComicsGrid).Position;

            var distance = Math.Sqrt(Math.Pow(tapPosition.X - lastPosition.X, 2) + Math.Pow(tapPosition.Y - lastPosition.Y, 2));
            this.doubleTapPointerReleased = distance < 10;
        }

        // Prepares the grid before the right click context menu is shown
        private void VisibleComicsGrid_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            if (!(sender is GridView grid)) {
                throw new ApplicationLogicException("Only ComicItemGrid should be able to call this event handler");
            }

            var tappedElement = (FrameworkElement)e.OriginalSource;

            if (!(tappedElement.DataContext is ComicItem comicItem)) {
                // The right click happened on an empty space
                return;
            }

            // Selection logic that behaves like most other reasonable apps
            if (!grid.SelectedItems.Contains(comicItem)) {
                grid.SelectedItems.Clear();
                grid.SelectedItems.Add(comicItem);
            }
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
            }

            // MainPage cannot rely on ContentFrame.Navigated because we navigate to a ComicItemGridContainer, not this class
            args.OnNavigatedTo?.Invoke(this, e);
        }

        internal void ManuallyNavigatedTo(NavigationEventArgs e) {
            this.OnNavigatedTo(e);
        }

        #endregion

        #region Context menu commands

        public ComicItemGridCommands ContextMenuCommands { get; }

        /* A note on keyboard shortcuts: KeyboardAccelerators seem to only run when the control responsible for the 
         * command is available. This translates to shortcuts only working then the command's CanExecute evaluated to 
         * true the last time the context menu flyout was shown. Since we can't get commands to consistently execute 
         * without rewriting the system, they are disabled for now. */

        public class ComicItemGridCommands {
            public XamlUICommand OpenItemsCommand { get; }
            public XamlUICommand SearchSelectedCommand { get; }

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

            // Update dynamic text when opening flyout
            foreach (var item in this.ComicItemGridContextFlyout.Items) {
                if (item.Tag != null && item is MenuFlyoutItem flyoutItem) {
                    flyoutItem.Text = this.GetDynamicFlyoutText(item.Tag.ToString());
                }
            }
        }

        private string GetDynamicFlyoutText(string tag) {
            var type = ((ComicItem)this.VisibleComicsGrid.SelectedItems[0]).ItemType;

            return tag switch
            {
                "open" => (type == ComicItemType.Work ? "Open" : "Navigate into") +
                          (this.VisibleComicsGrid.SelectedItems.Count > 1 ? $" {this.VisibleComicsGrid.SelectedItems.Count} items" : ""),
                "search" => "Search selected",
                _ => throw new ApplicationLogicException($"Unhandled tag name for flyout item: '{tag}'")
            };
        }

        #endregion

        #region Dynamic resizing

        /* We will have to manage item widths manually. Note that the Windows Community Toolkit provides the ability ]
         * to do this without writing custom code using AdaptiveGridView, but that uses a min width for each item, 
         * when we want a max width for each item. */
        private void VisibleComicsGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (sender is GridView grid) {
                this.RecalculateGridItemSize(grid);
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
    }
}