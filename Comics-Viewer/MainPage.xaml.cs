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
using MUXC = Microsoft.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ComicsViewer {
    public sealed partial class MainPage : Page {
        private ComicStore comicStore;
        private ComicItemGrid activeContent;

        public MainPage() {
            this.InitializeComponent();
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

        private async Task SwitchToProfile(string profileName) {
            if (!ProfileManager.LoadedProfiles.Contains(profileName)) {
                throw new ApplicationLogicException("The application should not allow the user to switch to a non-existent profile.");
            }

            // update internal modeling
            Defaults.SettingsAccessor.LastProfile = profileName;

            var profile = await ProfileManager.LoadProfile(profileName);
            this.comicStore = await ComicStore.CreateComicsStore(profile);

            // update UI
            /* Here's a brief description of what ProfileNavigationViewItem is:
             * It is a dropdown. The root element is the name of the current profile. Clicking on this element navigates
             * to the "All Items" (named "comics" internally) page of the currently loaded profile. The dropdown 
             * elements are the names of the other profiles that are loaded but not active. Clicking on one of those 
             * profile names switches to that profile. As a side effect switching profiles brings you to the "All Items"
             * page */
            this.ProfileNavigationViewItem.Content = profileName;
            this.ProfileNavigationViewItem.MenuItems.Clear();
            foreach (var existingProfile in ProfileManager.LoadedProfiles) {
                if (existingProfile != profileName) {
                    this.ProfileNavigationViewItem.MenuItems.Add(existingProfile);
                }
            }

            this.SelectedNavigationTag = this.ProfileNavigationViewItem.Tag.ToString();
            this.NavigateToTab(this.SelectedNavigationTag, new EntranceNavigationTransitionInfo());
        }

        /* Internally tracking navigation tag that doesn't need to be exposed to the view model */
        private string selectedNavigationTag;
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

            if (this.activeContent.ViewModel.PageType == tag) {
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

            var navigationArguments = this.GetNavigationArguments(this.comicStore.CreateViewModelForPage(null, tag));
            this.ContentFrame.Navigate(typeof(ComicItemGridTopLevelContainer), navigationArguments, transitionInfo);
        }


        private void NavigateToTab(string tag) 
            => this.NavigateToTab(tag, new EntranceNavigationTransitionInfo());

        /// <summary>
        /// Called when a search is updated. Refreshes the current top-level tab to apply the search by re-navigating to it.
        /// </summary>
        /// <param name="search">Filter function representing the search.</param>
        private void ReloadCurrentTabWithSearch(Func<Comic, bool> search) {
            this.navigationDepth = 0;

            var navigationArguments = this.GetNavigationArguments(this.comicStore.CreateViewModelForPage(search, this.activeContent.ViewModel.PageType));
            this.ContentFrame.Navigate(typeof(ComicItemGridTopLevelContainer), navigationArguments);
        }

        /// <summary>
        /// Called when the user clicks a ComicNavigationItem
        /// </summary>
        private void ComicItemGrid_RequestingNavigation(ComicItemGrid sender, RequestingNavigationEventArgs args) {
            switch (args.NavigationType) {
                case RequestingNavigationType.Into:
                    this.navigationDepth += 1;
                    var navigationArguments = this.GetNavigationArguments(this.comicStore.CreateViewModelForComics(args.NavigationItem.Comics));
                    this.ContentFrame.Navigate(typeof(ComicItemGridSecondLevelContainer), navigationArguments);
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

        private void NavigationView_BackRequested(MUXC.NavigationView sender, MUXC.NavigationViewBackRequestedEventArgs args) {
            this.NavigateOut();
        }

        private ComicItemGridNavigationArguments GetNavigationArguments(ComicViewModel viewModel) {
            return new ComicItemGridNavigationArguments {
                ViewModel = viewModel,
                OnNavigatedTo = this.ComicItemGrid_OnNavigatedTo
            };
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
            this.NavigationView.IsBackEnabled = this.navigationDepth > 0;
        }

        private void ContentFrame_NavigationFailed(object _, NavigationFailedEventArgs e) {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        #endregion

        #region Search

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) {
                // TODO: update AutoSuggestBox.ItemsSource to set list of auto-suggested items
                sender.ItemsSource = Search.GetSearchSuggestions(sender.Text);
            }
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) {
            // TODO: update sender.Text to reflect the selection the user has currently highlighted
            sender.Text = (string)args.SelectedItem;
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) {
            // TODO: submit the search
            var search = Search.Compile(sender.Text);

            if (search == null) {
                return;
            }

            // remove focus from the search box
            this.activeContent.Focus(FocusState.Pointer);
            this.ReloadCurrentTabWithSearch(search);
        }

        #endregion
    }
}
