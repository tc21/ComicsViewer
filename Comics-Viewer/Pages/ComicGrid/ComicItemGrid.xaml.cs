using ComicsLibrary;
using ComicsViewer.Profiles;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer {
    public sealed partial class ComicItemGrid : Page {
        public ComicViewModel? ViewModel;
        public ComicItemGrid() {
            this.InitializeComponent();
            this.ContextMenuCommands = new ComicItemGridCommands(this);
        }

        private Task OpenItem(ComicItem item) {
            if (item is ComicWorkItem workItem) {
                return Startup.OpenComic(workItem.Comic, this.ViewModel!.Profile);
            }

            // item is navigation item
            this.RequestNavigationInto((ComicNavigationItem)item);
            return Task.CompletedTask;
        }

        private void RequestNavigationInto(ComicNavigationItem item) {
            this.RequestingNavigation?.Invoke(this, new RequestingNavigationEventArgs {
                NavigationItem = item,
                NavigationType = RequestingNavigationType.Into
            });
        }

        private void RequestSearchResultOf(IEnumerable<Comic> comics) {
            this.RequestingNavigation?.Invoke(this, new RequestingNavigationEventArgs {
                NavigationItem = new ComicNavigationItem("<dynamically generated>", comics),
                NavigationType = RequestingNavigationType.Search
            });
        }

        public delegate void RequestingNavigationEventDelegate(ComicItemGrid sender, RequestingNavigationEventArgs args);
        public event RequestingNavigationEventDelegate? RequestingNavigation;

        private async void VisibleComicsGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs _) {
            if (!(sender is GridView grid)) {
                throw new ApplicationLogicException("Only ComicItemGrid should be able to call this event handler");
            }

            // do we really need to await this?
            await Task.WhenAll(this.VisibleComicsGrid.SelectedItems.Select(i => this.OpenItem((ComicItem)i)));
        }

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
                if (VisualTreeHelper.GetChild(this.VisibleComicsGrid, 0) is UIElement childElement &&
                    VisualTreeHelper.GetChild(childElement, 0) is ScrollViewer scrollViewer) {
                    scrollViewer.ChangeView(null, scrollViewer.VerticalOffset, null);
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

            if (e.NavigationMode == NavigationMode.New) {
                // Initialize this page only when creating a new page, 
                // not when the user returned to this page by pressing the back button
                this.ViewModel = args.ViewModel;
            }

            // Note: we must call this function no matter what. MainPage must handle things differently based on e.NavigationMode on its own.
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
            private IEnumerable<ComicWorkItem> WorkItems => parent.VisibleComicsGrid.SelectedItems.OfType<ComicWorkItem>();
            private IEnumerable<ComicNavigationItem> NavItems => parent.VisibleComicsGrid.SelectedItems.OfType<ComicNavigationItem>();
            // assuming all of the same type
            private ComicItemType SelectedItemType {
                get {
                    if (this.SelectedItemCount == 0) {
                        return ComicItemType.None;
                    }

                    if (this.parent.VisibleComicsGrid.SelectedItems[0] is ComicWorkItem) {
                        return ComicItemType.Work;
                    }

                    return ComicItemType.None;
                }
            }

            internal ComicItemGridCommands(ComicItemGrid parent) {
                this.parent = parent;

                // Opens selected comics or navigates into the selected navigation item
                this.OpenItemsCommand = new StandardUICommand(StandardUICommandKind.Open);
                this.OpenItemsCommand.ExecuteRequested += async (sender, args) 
                    => await Task.WhenAll(this.SelectedItems.Select(parent.OpenItem));
                this.OpenItemsCommand.CanExecuteRequested += this.CanExecuteHandler(() 
                    => this.SelectedItemType == ComicItemType.Work || this.SelectedItemCount == 1);

                // Generates and executes a search limiting visible items to those selected
                this.SearchSelectedCommand = new XamlUICommand();
                this.SearchSelectedCommand.ExecuteRequested += (sender, args) => {
                    parent.RequestSearchResultOf(this.SelectedItemType switch {
                        ComicItemType.Work => this.WorkItems.Select(i => i.Comic),
                        _ => this.NavItems.SelectMany(i => i.Comics)
                    });
                };
                this.SearchSelectedCommand.CanExecuteRequested += this.CanExecuteHandler(() => 
                    this.SelectedItemType == ComicItemType.Navigation || this.SelectedItemCount > 1);
            }

            private TypedEventHandler<XamlUICommand, CanExecuteRequestedEventArgs> CanExecuteHandler(Func<bool> predicate) {
                return (sender, args) => args.CanExecute = predicate();
            }

            private enum ComicItemType {
                Work, Navigation, None
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
            return tag switch
            {
                "open" => (this.VisibleComicsGrid.SelectedItems[0] is ComicWorkItem ? "Open" : "Navigate into") +
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

    public class RequestingNavigationEventArgs {
        public ComicNavigationItem? NavigationItem { get; set; }
        public RequestingNavigationType NavigationType { get; set; }
    }

    /* Handled at MainPage -> ComicItemGrid_RequestingNavigation */
    public enum RequestingNavigationType {
        Into, Search
    }

    public class BooleanToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            //reverse conversion (false=>Visible, true=>collapsed) on any given parameter
            var input = (null == parameter) ? (bool)value : !((bool)value);
            return input ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
}
