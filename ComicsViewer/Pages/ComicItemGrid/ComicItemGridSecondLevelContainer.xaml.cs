using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    /* This class is an exact duplicate of ComicItemGridTopLevelContainer. For why, see the comment for ComicItemGridTopLevelContainer. */
    public sealed partial class ComicItemGridSecondLevelContainer : IComicItemGridContainer {
        public ComicItemGrid? Grid { get; private set; }

        public ComicItemGridSecondLevelContainer() {
            this.InitializeComponent();
        }

        private void Frame_Navigated(object sender, NavigationEventArgs e) {
            this.Grid = (ComicItemGrid)this.Frame.Content!;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (e.NavigationMode == NavigationMode.Back) {
                this.Grid?.ManuallyNavigatedTo(e);
                return;
            }

            _ = this.Frame.Navigate(typeof(ComicItemGrid), e.Parameter, new SuppressNavigationTransitionInfo());
        }
    }
}
