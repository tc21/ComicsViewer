﻿using System;
using System.Collections.Generic;
using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.Uwp.Common;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public sealed partial class ComicWorkItemPage : Page, IMainPageContent {
        /* TODO
         *  - we should implement more commands in the 'more' menu, similar to the right click menu
         *  - we should implement scroll position saving/loading via the ComicItemGridCache */
        public ComicWorkItemPage() {
            this.InitializeComponent();
        }

        private ComicWorkItemPageViewModel? _viewModel;
        private ComicWorkItemPageViewModel ViewModel => this._viewModel ?? throw ProgrammerError.Unwrapped();
        private MainViewModel MainViewModel => this.ViewModel.MainViewModel;

        public NavigationTag NavigationTag => this.MainViewModel.ActiveNavigationTag;
        public string PageName => this.ViewModel.ComicItem.Title;

        public NavigationPageType NavigationPageType => this.MainViewModel.ActiveNavigationPageType;
        public Page Page => this;
        public ComicItemGrid? ComicItemGrid => null;

        public Action NavigateOut => () => {
            if (TryClosePortablePage(this.ComicsOverlayFrame)) {
                return;
            }

            this.MainViewModel.TryNavigateOut();
        };

        private bool useThumbnails = false;

        public event Action<IMainPageContent>? Initialized;

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is not ComicWorkItemPageNavigationArguments args) {
                throw new ProgrammerError("A ComicWorkItemPage must receive a ComicWorkItemPageNavigationArguments as its parameter.");
            }

            this._viewModel = args.ViewModel;

            await this.ViewModel.InitializeAsync();

            if (this.ViewModel.Initialized) {
                this.useThumbnails = this.ViewModel.ShouldUseThumbnails();
            }

            // After we change the visual state here, if the grid had already loaded, it may have initialized
            // with a wrong value for useThumbnails. So we call it again.
            // Note: Calling VisualStateManager.GoToState will set this.ComicSubitemGrid.ItemsPanelRoot to null (temporarily). 
            // For that purpose we must do this before setting visual state.
            if (this.ComicSubitemGrid.IsLoaded) {
                this.RecalculateGridItemSize(this.ComicSubitemGrid);
            }

            if (this.ViewModel.Subitems.Count == 0) {
                _ = VisualStateManager.GoToState(this, "NoSubitems", false);
            } else {
                if (this.useThumbnails) {
                    _ = VisualStateManager.GoToState(this, "ThumbnailsVisible", false);
                } else {
                    _ = VisualStateManager.GoToState(this, "ThumbnailsHidden", false);
                }
            }

            this.Initialized?.Invoke(this);

            switch (e.NavigationMode) {
                case NavigationMode.New:
                    // Note: We may change the HighlightedComicItem's thumbnail size by setting visual manager state,
                    // so we should start the connected animation after.
                    this.HighlightedComicItem.TryStartConnectedAnimationToThumbnail(this.ViewModel.ComicItem);
                    break;

                case NavigationMode.Back:
                    _ = ComicItemGridCache.PopStack(this.NavigationTag, this.PageName);
                    break;
                default:
                    throw new ProgrammerError("Unexpected navigation mode");
            }

            this.ViewModel.ComicItem.PropertyChanged += this.ComicItem_PropertyChanged;
            this.SynchronizeLovedStatusToUI();

            if (await this.ViewModel.TryLoadDescriptionsAsync(this.InfoTextBlock)) {
                this.ToggleInfoPaneButton.Visibility = Visibility.Visible;
            }

            if (this.ViewModel.Initialized && this.useThumbnails) {
                await this.ViewModel.LoadThumbnailsAsync();
            }
        }

        // TODO: maybe move this elsewhere
        private static bool TryOpenPortablePage<T>(Frame frame, ProtocolActivatedArguments arguments) where T : IPortablePage {
            if (frame.IsNavigationStackEnabled) {
                throw new ProgrammerError("A portable page can only be opened in a frame without a navigation stack");
            }

            if (frame.Content is IPortablePage page) {
                page.PrepareUnload();
            }

            frame.Navigated += Frame_Navigated;
            frame.Visibility = Visibility.Visible;

            if (!frame.Navigate(typeof(T), arguments)) {
                return false;
            }

            return true;

            void Frame_Navigated(object sender, NavigationEventArgs e) {
                if (e.Content is not T content) {
                    throw new ProgrammerError("Unexpected frame conetnt");
                }

                frame.Navigated -= Frame_Navigated;
            }
        }

        private static bool TryClosePortablePage(Frame frame) {
            if (frame.Content is IPortablePage page) {
                page.PrepareUnload();
                frame.Content = null;
                frame.Visibility = Visibility.Collapsed;
                return true;
            }

            return false;

        }

        public bool TryLaunchOverlayViewer<T>(ProtocolActivatedArguments arguments) where T : IPortablePage {
            return TryOpenPortablePage<MusicPlayer.MainPage>(this.ComicsOverlayFrame, arguments);
        }

        private void ComicItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName is nameof(ComicItem.IsLoved) or "") {
                this.SynchronizeLovedStatusToUI();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
            // For now, we push useless information onto the cache to keep everything else working
            if (e.NavigationMode is NavigationMode.New) {
                ComicItemGridCache.PushStack(this.NavigationTag, this.PageName, new ComicItemGridState(new(), 0, this.MainViewModel.LastModified));
            }

            // Ideally this should be automated
            this.ViewModel.RemoveEventHandlers();
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel.OpenComicItemAsync(this);
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e) {
            // TODO this code is copied from ComicItemGrid. We should evaluate whether it needs to be generalized.
            _ = await new PagedContentDialog { Title = "Edit info" }.NavigateAndShowAsync<
                    EditComicInfoDialogContent, EditComicInfoDialogNavigationArguments
                >(new(this.MainViewModel, this.ViewModel.ComicItem));
        }

        private async void LoveButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel.ToggleLovedStatus();
        }

        private async void ShowInExplorerFlyoutItem_Click(object sender, RoutedEventArgs e) {
            await Startup.OpenContainingFolderAsync(this.ViewModel.ComicItem.Comic);
        }

        private void ShowAuthorFlyoutItem_Click(object sender, RoutedEventArgs e) {
            // This animation has such a low chance of succeeding, that I feel I should just remove it...
            _ = this.HighlightedComicItem.PrepareConnectedAnimationFromThumbnail(this.ViewModel.ComicItem);
            this.MainViewModel.NavigateToAuthor(this.ViewModel.ComicItem.Comic.Author);
        }

        private void SynchronizeLovedStatusToUI() {
            var loved = this.ViewModel.ComicItem.IsLoved;

            if (loved) {
                _ = VisualStateManager.GoToState(this, "Loved", false);
                this.LoveButton.IsChecked = true;
            } else {
                _ = VisualStateManager.GoToState(this, "NotLoved", false);
                this.LoveButton.IsChecked = false;
            }
        }

        private void ToggleInfoPaneButton_Checked(object sender, RoutedEventArgs e) {
            _ = VisualStateManager.GoToState(this, "InfoVisible", true);
        }

        private void ToggleInfoPaneButton_Unchecked(object sender, RoutedEventArgs e) {
            _ = VisualStateManager.GoToState(this, "InfoHidden", true);
        }

        private async void ComicSubitemGrid_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is not ComicSubitemContainer container) {
                throw new ProgrammerError("ComicSubitemGrid should only contain ComicSubitemContainer objects.");
            }

            await this.ViewModel.OpenComicItemAsync(this, container.Subitem);
        }

        private void ComicSubitemGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            this.RecalculateGridItemSize(this.ComicSubitemGrid);
        }

        private void RecalculateGridItemSize(GridView grid) {
            var itemsWrapGrid = (ItemsWrapGrid)grid.ItemsPanelRoot!;

            if (this.useThumbnails) {
                // TODO this code is also copied from ComicItemGrid. We should evaluate whether it needs to be generalized.
                var idealItemWidth = this.ViewModel.ImageWidth;
                var idealItemHeight = this.ViewModel.ImageHeight;
                var columns = Math.Ceiling(grid.ActualWidth / idealItemWidth);
                itemsWrapGrid.ItemWidth = grid.ActualWidth / columns;
                itemsWrapGrid.ItemHeight = itemsWrapGrid.ItemWidth * idealItemHeight / idealItemWidth;
            } else {
                itemsWrapGrid.ItemWidth = grid.ActualWidth;
            }
        }

        private void ComicSubitemGrid_Loaded(object sender, RoutedEventArgs e) {
            this.RecalculateGridItemSize(this.ComicSubitemGrid);

            // TODO: we should probably implement scrolling to saved offsets.
        }
    }
}
