using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ComicsViewer.ViewModels;
using ComicsViewer.Profiles;
using Windows.UI.Xaml.Media.Animation;
using System.Threading.Tasks;
using ComicsLibrary;
using ComicsViewer.ComicGrid;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using ComicsViewer.Pages;
using ComicsViewer.Filters;
using MUXC = Microsoft.UI.Xaml.Controls;
using MUXM = Microsoft.UI.Xaml.Media;

#nullable enable

namespace ComicsViewer {
    public sealed partial class MainPage : Page {
        private ComicStore comicStore = ComicStore.EmptyComicStore;
        private ComicItemGrid? activeContent;

        // stored to update BackButtonVisibility
        private readonly SystemNavigationManager currentView = SystemNavigationManager.GetForCurrentView();

        public MainPage() {
            this.InitializeComponent();

            // Custom title bar
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            coreTitleBar.LayoutMetricsChanged += this.CoreTitleBar_LayoutMetricsChanged;

            // Transparent upper-right-area buttons
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Windows.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Windows.UI.Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 255, 255, 255);

            // Enable back button on title bar
            this.currentView.BackRequested += this.CurrentView_BackRequested;
        }

        private string DefaultNavigationTag => this.ProfileNavigationViewItem.Tag.ToString();

        private async Task SwitchToProfile(string profileName) {
            if (!ProfileManager.LoadedProfiles.Contains(profileName)) {
                throw new ApplicationLogicException("The application should not allow the user to switch to a non-existent profile.");
            }

            // update internal modeling
            Defaults.SettingsAccessor.LastProfile = profileName;

            var profile = await ProfileManager.LoadProfile(profileName);
            this.comicStore = await ComicStore.CreateComicsStore(profile);
            this.comicFilter = new Filter();
            this.comicFilter.FilterChanged += this.ComicFilter_FilterChanged;

            // update UI
            /* Here's a brief description of what ProfileNavigationViewItem is:
             * It is a dropdown. The root element is the name of the current profile. Clicking on this element navigates
             * to the "All Items" (named "comics" internally) page of the currently loaded profile. The dropdown 
             * elements are the names of the other profiles that are loaded but not active. Clicking on one of those 
             * profile names switches to that profile. As a side effect switching profiles brings you to the "All Items"
             * page */
            this.SearchBox.Text = "";
            this.ProfileNavigationViewItem.Content = profileName;
            this.ProfileNavigationViewItem.MenuItems.Clear();
            foreach (var existingProfile in ProfileManager.LoadedProfiles) {
                if (existingProfile != profileName) {
                    this.ProfileNavigationViewItem.MenuItems.Add(existingProfile);
                }
            }

            this.SelectedNavigationTag = this.DefaultNavigationTag;
            this.NavigateToTab(this.SelectedNavigationTag, new EntranceNavigationTransitionInfo());

            this.NavigationView.SelectedItem = this.ProfileNavigationViewItem;
        }

        #region Navigation

        private async void NavigationView_Loaded(object _, RoutedEventArgs e) {
            /* Note: The app currently doesn't support multiple pages, but it one day might. */
            if (!ProfileManager.Initialized) {
                await ProfileManager.Initialize();
            }

            var profileName = Defaults.SettingsAccessor.LastProfile;
            if (!ProfileManager.LoadedProfiles.Contains(profileName)) {
                if (ProfileManager.LoadedProfiles.Count == 0) {
                    throw new ApplicationLogicException("The application in its current state only allows using pre-made profiles.");
                }

                profileName = ProfileManager.LoadedProfiles[0];
            }

            await this.SwitchToProfile(profileName);
        }

        /* Internally tracking navigation tag that doesn't need to be exposed to the view model */
        private string selectedNavigationTag = "";
        private string SelectedNavigationTag {
            get => this.selectedNavigationTag;
            set {
                if (this.selectedNavigationTag == value) {
                    return;
                }

                this.selectedNavigationTag = value;
                this.SelectedNavigationTagChanged(value);
            }
        }

        private void SelectedNavigationTagChanged(string newValue) {
            this.NavigateToTab(newValue, new EntranceNavigationTransitionInfo());
        }

        private async void NavigationView_ItemInvoked(MUXC.NavigationView sender, MUXC.NavigationViewItemInvokedEventArgs args) {
            /* There are two types of navigation view items that can be invoked:
             * 1. A "profile switch" item: Tag = null, Content = <profile name>
             * 2. A "navigate" item: Tag = <page type>
             * 
             * I don't like it cause it seems hacky, but whatever for now
             */
            if (args.InvokedItemContainer.Tag == null) {
                var profileName = args.InvokedItemContainer.Content.ToString();
                await this.SwitchToProfile(profileName);
                return;
            }

            var tag = args.InvokedItemContainer.Tag.ToString();

            if (this.activeContent?.ViewModel!.PageType == tag) {
                this.activeContent.ScrollToTop();
                return;
            }

            // We cheat a little here to maintain the recommended transition info
            this.selectedNavigationTag = tag;
            this.NavigateToTab(tag, args.RecommendedNavigationTransitionInfo);
        }

        private int navigationDepth = 0;

        /// <summary>
        /// Called when the user clicks one of the navigation tabs at the top of the page
        /// </summary>
        /// <param name="tag">The name of the tab the user clicked</param>
        private void NavigateToTab(string tag, NavigationTransitionInfo transitionInfo) {
            this.navigationDepth = 0;

            var navigationArguments = this.GetNavigationArguments(this.comicStore.CreateViewModelForPage(this.comicFilter, tag));
            this.ContentFrame.Navigate(typeof(ComicItemGridTopLevelContainer), navigationArguments, transitionInfo);
        }


        private void NavigateToTab(string tag) 
            => this.NavigateToTab(tag, new EntranceNavigationTransitionInfo());

        /// <summary>
        /// Called when a search is updated. Refreshes the current top-level tab to apply the search by re-navigating to it.
        /// </summary>
        /// <param name="search">Filter function representing the search.</param>
        private void ReloadCurrentTab() {
            var item = this.NavigationView.SelectedItem as MUXC.NavigationViewItem;
            this.NavigateToTab(item?.Tag?.ToString() ?? this.DefaultNavigationTag);
        }

        /// <summary>
        /// Called when the user clicks a ComicNavigationItem
        /// </summary>
        private void ComicItemGrid_RequestingNavigation(ComicItemGrid sender, RequestingNavigationEventArgs args) {
            switch (args.NavigationType) {
                case RequestingNavigationType.Into:
                    this.navigationDepth += 1;
                    var navigationArguments = this.GetNavigationArguments(this.comicStore.CreateViewModelForComics(args.NavigationItem!.Comics));
                    this.ContentFrame.Navigate(typeof(ComicItemGridSecondLevelContainer), navigationArguments);
                    return;
                case RequestingNavigationType.Search:
                    var predeterminedSearchResult = args.NavigationItem!.Comics.ToHashSet();
                    this.comicFilter!.Metadata.GeneratedFilterItemCount = predeterminedSearchResult.Count;
                    this.comicFilter!.GeneratedFilter = comic => predeterminedSearchResult.Contains(comic);
                    this.ReloadCurrentTab();
                    return;
            }

            throw new ApplicationLogicException("Unhandled RequestingNavigationType");
        }

        private void NavigateOut() {
            if (!this.ContentFrame.CanGoBack || this.navigationDepth == 0) {
                throw new ApplicationLogicException("Should not be possible to navigate out when there is no page to navigate back to.");
            }

            this.navigationDepth -= 1;
            this.ContentFrame.GoBack();
        }

        private void CurrentView_BackRequested(object sender, BackRequestedEventArgs e) {
            this.NavigateOut();
        }

        private ComicItemGridNavigationArguments GetNavigationArguments(ComicViewModel viewModel) {
            return new ComicItemGridNavigationArguments(viewModel, this.ComicItemGrid_OnNavigatedTo);
        }

        /// <summary>
        /// Callback method passed to a ComicItemGrid as part of ComicItemGridNavigationArguments.
        /// Called when a grid has loaded its view model.
        /// Sets some variables to "enable" the loaded grid by integrating it with this page.
        /// </summary>
        /// <param name="grid">The grid that finished loading.</param>
        private void ComicItemGrid_OnNavigatedTo(ComicItemGrid grid, NavigationEventArgs e) {
            if (e.NavigationMode == NavigationMode.New) {
                // We need to hook up this function here as part of ComicItemGrid initialization
                grid.RequestingNavigation += this.ComicItemGrid_RequestingNavigation;
            }

            this.activeContent = grid;
            this.currentView.AppViewBackButtonVisibility = 
                this.navigationDepth > 0 ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Disabled;
        }

        private void ContentFrame_NavigationFailed(object _, NavigationFailedEventArgs e) {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        #endregion

        #region Search

        private Filter? comicFilter;

        private void ComicFilter_FilterChanged(Filter filter) {
            this.ReloadCurrentTab();

            if (this.comicFilter?.GeneratedFilter == null) {
                this.FilterButton.Background = null;
            } else {
                this.FilterButton.Background = this.Resources["SystemControlAccentAcrylicElementAccentMediumHighBrush"] as Brush;
            }
            
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
            if (sender.Text.Trim() == "") {
                sender.ItemsSource = Defaults.SettingsAccessor.SavedSearches;
                return;
            }
            
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) {
                sender.ItemsSource = Search.GetSearchSuggestions(sender.Text).ToList();
                return;
            }
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) {
            sender.Text = (string)args.SelectedItem;
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) {
            var search = Search.Compile(sender.Text);

            if (search == null) {
                return;
            }

            // Add this search to the recents list
            if (sender.Text.Trim() != "") {
                var savedSearches = Defaults.SettingsAccessor.SavedSearches;
                RemoveIgnoreCase(ref savedSearches, sender.Text);
                savedSearches.Insert(0, sender.Text);

                while (savedSearches.Count > 4) {
                    savedSearches.RemoveAt(4);
                }

                Defaults.SettingsAccessor.SavedSearches = savedSearches;
            }

            // remove focus from the search box (partially to indicate that the search succeeded)
            this.activeContent?.Focus(FocusState.Pointer);

            // execute the search
            this.comicFilter!.Metadata.SearchPhrase = sender.Text;
            this.comicFilter!.Search = search;

            // Helper functions
            static void RemoveIgnoreCase(ref IList<string> list, string text) {
                var removes = new List<int>();

                for (var i = 0; i < list.Count; i++) {
                    if (list[i].Equals(text, StringComparison.OrdinalIgnoreCase)) {
                        removes.Insert(0, i);
                    }
                }

                foreach (var i in removes) {
                    list.RemoveAt(i);
                }
            }
        }

        #endregion

        /* reference: https://docs.microsoft.com/en-us/windows/uwp/design/shell/title-bar#full-customization-example */
        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args) {
            this.LeftPaddingColumn.Width = new GridLength(sender.SystemOverlayLeftInset);
            this.RightPaddingColumn.Width = new GridLength(sender.SystemOverlayRightInset);

            this.AppTitleBar.Height = sender.Height;
        }

        private void FilterNavigationViewItem_Tapped(object sender, TappedRoutedEventArgs e) {
            var flyout = (this.Resources["FilterFlyout"] as Flyout)!;
            this.FilterFlyoutFrame.Navigate(typeof(FilterPage), new FilterPageNavigationArguments(this.activeContent!.ViewModel!, this.comicFilter));
            flyout.ShowAt(sender as FrameworkElement);
        }
    }
}
