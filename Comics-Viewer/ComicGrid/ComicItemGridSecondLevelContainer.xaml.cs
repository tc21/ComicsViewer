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

namespace ComicsViewer.ComicGrid {
    /* This class is an exact duplicate of ComicItemGridTopLevelContainer. For why, see the comment for ComicItemGridTopLevelContainer. */
    public sealed partial class ComicItemGridSecondLevelContainer : Page, IComicItemGridContainer {
        public ComicItemGrid Grid { get; private set; }

        public ComicItemGridSecondLevelContainer() {
            this.InitializeComponent();
        }

        private void Frame_Navigated(object sender, NavigationEventArgs e) {
            this.Grid = (ComicItemGrid)this.Frame.Content;

            Debug.WriteLine($"SecondLevelContainer.Frame.Navigated");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (e.NavigationMode == NavigationMode.Back) {
                // reuse cached page when navigating back
                this.Grid.ManuallyNavigatedTo(e);
                return;
            }

            Debug.WriteLine($"SecondLevelContainer.OnNavigatedTo");

            this.Frame.Navigate(typeof(ComicItemGrid), e.Parameter, new SuppressNavigationTransitionInfo());
        }
    }
}
