using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.ComicGrid {
    /* UWP cannot handle caching multiple instances of pages of the same type. To enable caching pages using Frame's
     * built-in behavior, each page must be a different type. Here we limit the application to two pages. Each container 
     * page contains a grid, and the container page is cached instead of the grid. When navigating to the container,
     * the navigation mode in the NavigationEventArgs argument will tell us if we need to reload the page.
     * 
     * For reference, here is the current list of changes:
     *  1. The page contains a Frame, named Frame
     *  2. The page sets NavigationCacheMode to Enabled
     *  3. Implement IComicItemGridContainer, to expose the contained ComicItemGrid
     *  4. The exposed grid is set on Frame.Navigated
     *  5. OnNavigatedTo navigates to a new grid, except when the cached grid is wanted (during back-navigation).
     *  
     * Note: Currently SecondLevelContainers never reload. We could just not do part 2 and part 5's "except" for second level grids.
     * But just in case we want to in the future, I want to make it obvious that they should have the exact same implementation.
     */
    public sealed partial class ComicItemGridTopLevelContainer : Page, IComicItemGridContainer {
        public ComicItemGrid? Grid { get; private set; }

        public ComicItemGridTopLevelContainer() {
            this.InitializeComponent();
        }

        private void Frame_Navigated(object sender, NavigationEventArgs e) {
            this.Grid = (ComicItemGrid)this.Frame.Content;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (e.NavigationMode == NavigationMode.Back && this.Grid != null) {
                // reuse cached page when navigating back
                // Note, since ComicItemGridContainer is just a wrapper around ComicItemGrid, and all the logic is 
                // handled by ComicItemGrid, we have make sure ComicItemGrid.OnNavigatedTo is called no matter what.
                this.Grid?.ManuallyNavigatedTo(e);
                return;
            }

            //Debug.WriteLine($"TopLevelContainer.OnNavigatedTo");

            this.Frame.Navigate(typeof(ComicItemGrid), e.Parameter, new SuppressNavigationTransitionInfo());
        }
    }
}
