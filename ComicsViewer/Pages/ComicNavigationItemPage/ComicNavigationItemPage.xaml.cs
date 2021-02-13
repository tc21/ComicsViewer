using System;
using ComicsViewer.Common;
using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public sealed partial class ComicNavigationItemPage : Page, IMainPageContent {
        public ComicNavigationItemPage() {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is not ComicNavigationItemPageNavigationArguments args) {
                throw new ProgrammerError("A ComicRootPage must receive a ComicNavigationItemPageNavigationArguments as its parameter.");
            }

            if (this.IsInitialized) {
                return;
            }

            this._navigationTag = args.NavigationTag;

            var viewModel = ComicItemGridViewModel.ForSecondLevelNavigationTag(this, args.MainViewModel, args.Comics, args.Properties);
            this.ComicsCount = viewModel.TotalItemCount;

            this.IsInitialized = true;
            this.Initialized?.Invoke(this);

            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                this.InnerContentFrame.Navigate(typeof(ComicItemGrid), new ComicItemGridNavigationArguments { ViewModel = viewModel }));
        }

        private NavigationTag? _navigationTag;

        public NavigationTag NavigationTag => _navigationTag ?? throw ProgrammerError.Unwrapped();
        public NavigationPageType NavigationPageType => NavigationPageType.NavigationItem;
        public Page Page => this;
        public int ComicsCount { get; private set; } = 0;
        public ComicItemGrid? ComicItemGrid { get; private set; }

        public bool IsInitialized { get; private set; }
        public event Action<IMainPageContent>? Initialized;

        private void InnerContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e) {
            throw new ProgrammerError();
        }

        private void InnerContentFrame_Navigated(object sender, NavigationEventArgs e) {
            this.ComicItemGrid = (ComicItemGrid)e.Content;
        }
    }
}
