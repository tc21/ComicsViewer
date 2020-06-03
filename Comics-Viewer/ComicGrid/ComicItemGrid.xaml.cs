using ComicsViewer.Profiles;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace ComicsViewer {
    public sealed partial class ComicItemGrid : Page {
        public ComicViewModel ViewModel;
        public ComicItemGrid() {
            this.InitializeComponent();
        }

        async void VisibleComicsGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs _) {
            if (!(sender is GridView grid)) {
                throw new ApplicationLogicException("Only ComicItemGrid should be able to call this event handler");
            }

            foreach (var item in grid.SelectedItems) {
                if (item is ComicWorkItem comicWorkItem) {
                    await Startup.OpenComic(comicWorkItem.Comic, this.ViewModel.Profile);
                }

                if (item is ComicNavigationItem comicNavigationItem) {
                    this.RequestNavigationInto(comicNavigationItem);
                }
            }
        }

        void RequestNavigationInto(ComicNavigationItem item) {
            this.RequestingNavigation(this, new RequestingNavigationEventArgs {
                NavigationItem = item,
                NavigationType = RequestingNavigationType.Into
            });
        }

        internal delegate void RequestingNavigationEventDelegate(ComicItemGrid sender, RequestingNavigationEventArgs args);
        internal event RequestingNavigationEventDelegate RequestingNavigation;

        #region Controlling from MainPage

        internal void ScrollToTop() {
            if (this.ViewModel.ComicItems.Count > 0) {
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
            args.OnNavigatedTo(this, e);
        }

        internal void ManuallyNavigatedTo(NavigationEventArgs e) {
            this.OnNavigatedTo(e);
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
            var idealItemWidth = this.ViewModel.ImageWidth;
            var idealItemHeight = this.ViewModel.ImageHeight;
            var columns = Math.Ceiling(grid.ActualWidth / idealItemWidth);
            var itemsWrapGrid = (ItemsWrapGrid)grid.ItemsPanelRoot;
            itemsWrapGrid.ItemWidth = grid.ActualWidth / columns;
            itemsWrapGrid.ItemHeight = itemsWrapGrid.ItemWidth * idealItemHeight / idealItemWidth;
        }

        #endregion
    }

    class RequestingNavigationEventArgs {
        internal ComicNavigationItem NavigationItem { get; set; }
        internal RequestingNavigationType NavigationType { get; set; }
    }

    enum RequestingNavigationType {
        Into
    }
}
