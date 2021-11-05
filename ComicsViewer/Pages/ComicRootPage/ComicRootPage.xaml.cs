using System;
using ComicsViewer.Common;
using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    // ComicRoot page only includes a ComicItemGrid, and nothing else;
    public sealed partial class ComicRootPage : Page, IMainPageContent {
        public ComicRootPage() {
            this.InitializeComponent();
        }

        private ComicItemGridViewModel? _viewModel;
        private ComicItemGridViewModel ViewModel => this._viewModel ?? throw ProgrammerError.Unwrapped();

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is not ComicRootPageNavigationArguments args) {
                throw new ProgrammerError("A ComicRootPage must receive a ComicRootPageNavigationArguments as its parameter.");
            }

            this._navigationTag = args.NavigationTag;

            var savedState = ComicItemGridCache.GetRoot(args.NavigationTag);

            // We only want to restore the scrollviewer position if the user navigates *back* to this page.
            if (savedState is not null && e.NavigationMode is NavigationMode.New) {
                savedState.ScrollOffset = 0;
            }

            this._viewModel = ComicItemGridViewModel.ForTopLevelNavigationTag(this, args.MainViewModel, savedState);
            this.ComicsCount = this.ViewModel.TotalItemCount;

            this.Initialized?.Invoke(this);

            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                this.InnerContentFrame.Navigate(typeof(ComicItemGrid), new ComicItemGridNavigationArguments {
                    ViewModel = ViewModel,
                    OnNavigatedTo = (grid, _) => this.ComicItemGrid = grid
                }));
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
            if (this.ComicItemGrid is null) {
                throw new ProgrammerError("Navigating out of a ComicNavigationItemPage that wasn't initialized.");
            }

            ComicItemGridCache.PutRoot(this.NavigationTag, this.ComicItemGrid.GetSaveState());

            // Ideally this should be automated
            this.ViewModel.RemoveEventHandlers();
            this.ComicItemGrid.DisposeAndInvalidate();
        }

        private NavigationTag? _navigationTag;

        public NavigationTag NavigationTag => this._navigationTag ?? throw ProgrammerError.Unwrapped();
        public NavigationPageType NavigationPageType => NavigationPageType.Root;
        public Page Page => this;
        public int ComicsCount { get; private set; } = 0;
        public ComicItemGrid? ComicItemGrid { get; private set; }
        public string? PageName => null;

        public event Action<IMainPageContent>? Initialized;
        public EventHandler<BackRequestedEventArgs>? BackRequested => null;

        public Action NavigateOut => this.ViewModel.MainViewModel.TryNavigateOut;

        private void InnerContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e) {
            throw new ProgrammerError("ComicRootPage: Navigation failed");
        }
    }
}
