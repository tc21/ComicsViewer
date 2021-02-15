using System;
using ComicsViewer.Common;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public sealed partial class ComicNavigationItemPage : Page, IMainPageContent {
        public ComicNavigationItemPage() {
            this.InitializeComponent();
        }

        private ComicNavigationItem? _comicItem;
        public ComicNavigationItem ComicItem => this._comicItem ?? throw ProgrammerError.Unwrapped();

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is not ComicNavigationItemPageNavigationArguments args) {
                throw new ProgrammerError("A ComicRootPage must receive a ComicNavigationItemPageNavigationArguments as its parameter.");
            }

            this._navigationTag = args.NavigationTag;
            this._comicItem = args.ComicItem;

            var savedState = e.NavigationMode switch {
                NavigationMode.New => null,
                NavigationMode.Back => ComicItemGridCache.PopStack(this.NavigationTag, this.ComicItem.Title),
                _ => throw new ProgrammerError("Unexpected switch case"),
            };

            var viewModel = ComicItemGridViewModel.ForSecondLevelNavigationTag(this, args.MainViewModel, args.ComicItem.Comics, args.Properties, savedState);
            this.ComicsCount = viewModel.TotalItemCount;

            this.Initialized?.Invoke(this);

            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                this.InnerContentFrame.Navigate(typeof(ComicItemGrid), new ComicItemGridNavigationArguments { 
                    ViewModel = viewModel, 
                    HighlightedComicItem = args.ComicItem 
                })
            );
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
            if (this.ComicItemGrid is null) {
                throw new ProgrammerError("Navigating out of a ComicNavigationItemPage that wasn't initialized.");
            }

            if (e.NavigationMode is NavigationMode.New) {
                ComicItemGridCache.PushStack(this.NavigationTag, this.ComicItem.Title, this.ComicItemGrid.GetSaveState());
            }
        }

        private NavigationTag? _navigationTag;

        public NavigationTag NavigationTag => _navigationTag ?? throw ProgrammerError.Unwrapped();
        public NavigationPageType NavigationPageType => NavigationPageType.NavigationItem;
        public Page Page => this;
        public int ComicsCount { get; private set; } = 0;
        public ComicItemGrid? ComicItemGrid { get; private set; }

        public event Action<IMainPageContent>? Initialized;

        private void InnerContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e) {
            throw new ProgrammerError();
        }

        private void InnerContentFrame_Navigated(object sender, NavigationEventArgs e) {
            this.ComicItemGrid = (ComicItemGrid)e.Content;
            this.ComicItemGrid.FinishNavigateInConnectedAnimationIfExists(this.ComicItem);
        }
    }
}
