using System;
using System.Linq;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Pages;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using MUXC = Microsoft.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer {
    /* Note: MainPage does not have its own view model because how simple its logic is. In the future when we complicate 
     * its logic, we may want a separate view model class. (This also means we are allowed to communicate a little with 
     * the models here) */
    public sealed partial class MainPage {
        private IMainPageContent? page;

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

        // Public because it's used in the xaml
        public readonly MainViewModel ViewModel = new();

        #region Navigation

        private async void NavigationView_Loaded(object sender, RoutedEventArgs e) {
            /* Note: The app currently doesn't support multiple pages, but it one day might. */
            if (!ProfileManager.Initialized) {
                await ProfileManager.InitializeAsync();
            }

            this.ViewModel.ProfileChanged += this.ViewModel_ProfileChanged;
            this.ViewModel.NavigationRequested += this.ViewModel_NavigationRequested;

            // Initialize view models and fire events for the first time
            await this.ViewModel.SetDefaultProfileAsync();
        }

        private void ViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            if (e.ChangeType != ProfileChangeType.ProfileChanged) {
                return;
            }

            // update UI
            /* Here's a brief description of what ProfileNavigationViewItem is:
             * It is a dropdown. The root element is the name of the current profile. Clicking on this element navigates
             * to the "All Items" (named "comics" internally) page of the currently loaded profile. The dropdown 
             * elements are the names of the other profiles that are loaded but not active. Clicking on one of those 
             * profile names switches to that profile. As a side effect switching profiles brings you to the "All Items"
             * page */
            this.SearchBox.Text = "";
            this.ProfileNavigationViewItem.Content = e.NewProfile.Name;
            this.ProfileNavigationViewItem.MenuItems.Clear();
            foreach (var existingProfile in ProfileManager.LoadedProfiles) {
                if (existingProfile != e.NewProfile.Name) {
                    // Note: I switched from adding strings to adding NavigationViewItems because randomly, clicking on
                    // the profiles dropdown would crash. Some unknown error would occur in the code responsible for creating
                    // UI items, but I wasn't able to figure out more than that. It seems like explicitly creating the 
                    // NavigationViewItems has fixed the problem for now.
                    var menuItem = new MUXC.NavigationViewItem { Content = existingProfile };
                    this.ProfileNavigationViewItem.MenuItems.Add(menuItem);
                }
            }

            this.NavigationView.SelectedItem = this.ProfileNavigationViewItem;

            // This will fire ViewModel.NavigationRequested. 
            // requireRefresh: true bypasses the default behavior of scrolling up to the top when "reloading"
            this.ViewModel.Navigate(NavigationTag.Comics, requireRefresh: true);
        }

        private async void ViewModel_NavigationRequested(MainViewModel sender, NavigationRequestedEventArgs e) {
            switch (e.NavigationType) {
                case NavigationType.In: {
                    if (e.NavigationPageType is NavigationPageType.Root) {
                        throw new ProgrammerError("NavigationPageType must be NavigationItem or WorkItem when navigating in");
                    }

                    var transitionInfo = e.TransitionInfo ?? new DrillInNavigationTransitionInfo();

                    switch (e.NavigationPageType) {
                        case NavigationPageType.NavigationItem: {
                            if (e.ComicItem is not ComicNavigationItem item) {
                                throw new ProgrammerError("ComicItem must be a navigation item");
                            }

                            var navigationArguments = new ComicNavigationItemPageNavigationArguments(this.ViewModel, e.NavigationTag, item, e.Properties);
                            _ = this.ContentFrame.Navigate(typeof(ComicNavigationItemPage), navigationArguments, transitionInfo);
                            break;
                        }

                        case NavigationPageType.WorkItem: {
                            if (e.ComicItem is not ComicWorkItem item) {
                                throw new ProgrammerError("ComicItem must be a work item");
                            }

                            var navigationArguments = new ComicWorkItemPageNavigationArguments(new ComicWorkItemPageViewModel(this.ViewModel, item));
                            _ = this.ContentFrame.Navigate(typeof(ComicWorkItemPage), navigationArguments, transitionInfo);
                            break;
                        }

                        default:
                            throw new ProgrammerError("NavigationPageType must be NavigationItem or WorkItem when navigating in");
                    }

                    break;
                }

                case NavigationType.Out:
                    this.ContentFrame.GoBack();
                    break;

                case NavigationType.New: {
                    if (e.NavigationPageType is not NavigationPageType.Root) {
                        throw new ProgrammerError("NavigationPageType must be Root when creating a new page");
                    }

                    var transitionInfo = e.TransitionInfo ?? new EntranceNavigationTransitionInfo();
                    var navigationArguments = new ComicRootPageNavigationArguments(this.ViewModel, e.NavigationTag);

                    this.ContentFrame.BackStack.Clear();
                    _ = this.ContentFrame.Navigate(typeof(ComicRootPage), navigationArguments, transitionInfo);

                    break;
                }

                case NavigationType.ScrollToTop:
                    if (this.page?.ComicItemGrid is { } grid) {
                        await grid.ScrollToAbsoluteOffsetAsync(0, true);
                    }

                    break;

                default:
                    throw new ProgrammerError($"Unhandled NavigationType '{e.NavigationType}'.");
            }
        }

        private async void NavigationView_ItemInvoked(MUXC.NavigationView sender, MUXC.NavigationViewItemInvokedEventArgs args) {
            if (args.IsSettingsInvoked) {
                // Don't navigate to settings twice
                if (this.ContentFrame.Content is SettingsPage) {
                    return;
                }

                // We need to make sure to increment NavigationDepth since we're manually calling ContentFrame.Navigate
                this.ViewModel.NavigationDepth += 1;
                _ = this.ContentFrame.Navigate(typeof(SettingsPage), new SettingsPageNavigationArguments(this.ViewModel, this.ViewModel.Profile));
                return;
            }

            if (args.InvokedItem == null) {
                // Don't know why this happens yet, but it happens when you select "continue running task" and then switch profiles after that.
                return;
            }

            /* There are two types of navigation view items that can be invoked:
             * 1. A "profile switch" item: Tag = null, Content = <profile name>
             * 2. A "navigate" item: Tag = <page type>
             * 
             * I don't like it cause it seems hacky, but whatever for now
             */
            if (args.InvokedItemContainer.Tag == null) {
                var profileName = args.InvokedItemContainer.Content!.ToString();
                await this.ViewModel.SetProfileAsync(profileName);
                return;
            }

            var tag = NavigationTags.FromTagName(args.InvokedItemContainer.Tag.ToString());
            this.ViewModel.Navigate(tag, args.RecommendedNavigationTransitionInfo);
        }

        private void CurrentView_BackRequested(object sender, BackRequestedEventArgs e) {
            this.ViewModel.TryNavigateOut();
        }

        private void ContentFrame_NavigationFailed(object _, NavigationFailedEventArgs e) {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void ContentFrame_Navigated(object _, NavigationEventArgs e) {
            // We can't just check for ContentFrame.CanGoBack, because the root page is pushed on to the back stack,
            // and we cannot navigate "back" from it.
            this.currentView.AppViewBackButtonVisibility = this.ViewModel.NavigationDepth > 0
                ? AppViewBackButtonVisibility.Visible
                : AppViewBackButtonVisibility.Disabled;

            if (e.Content is not IMainPageContent page) {
                // page may not be initialized, or we may be on a Settings page

                this.page = null;
                this.ViewModel.SetPageName(null);
                return;
            }

            page.Initialized += (page) => {
                this.page = page;
                this.ViewModel.ActiveNavigationPageType = page.NavigationPageType;
                this.ViewModel.ActiveNavigationTag = page.NavigationTag;
                this.ViewModel.SetPageName(page.PageName);
            };
        }

        #endregion

        #region Search

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) {
                return;
            }

            var suggestions = Search.GetSearchSuggestions(sender.Text, this.ViewModel.Profile.Name).ToList();
            while (suggestions.Count > 4) {
                suggestions.RemoveAt(4);
            }

            sender.ItemsSource = suggestions;

            if (Search.Compile(sender.Text) is { } search) {
                this.ViewModel.SubmitSearch(search, sender.Text, rememberSearch: false);
            }

        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) {
            sender.Text = (string)args.SelectedItem;
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) {
            if (Search.Compile(sender.Text) is not { } search) {
                return;
            }

            this.ViewModel.SubmitSearch(search, sender.Text);

            // remove focus from the search box (partially to indicate that the search succeeded)
            _ = this.page?.Page.Focus(FocusState.Pointer);
        }


        private void AutoSuggestBox_GotFocus(object sender, RoutedEventArgs e) {
            var suggestions = Search.GetSearchSuggestions(this.SearchBox.Text, this.ViewModel.Profile.Name).ToList();
            while (suggestions.Count > 4) {
                suggestions.RemoveAt(4);
            }

            this.SearchBox.ItemsSource = suggestions;
            this.SearchBox.IsSuggestionListOpen = true;
        }

        #endregion

        /* reference: https://docs.microsoft.com/en-us/windows/uwp/design/shell/title-bar#full-customization-example */
        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args) {
            this.LeftPaddingColumn.Width = new GridLength(sender.SystemOverlayLeftInset);
            this.RightPaddingColumn.Width = new GridLength(sender.SystemOverlayRightInset);

            this.AppTitleBar.Height = sender.Height;
        }

        private void TaskProgressButton_Tapped(object sender, TappedRoutedEventArgs e) {
            // We just allow the context flyout to be shown with a left click
            this.TaskProgressButton.ContextFlyout.ShowAt(this.TaskProgressButton);
        }

        private void MainGrid_PointerPressed(object sender, PointerRoutedEventArgs e) {
            var properties = e.GetCurrentPoint(this.MainGrid).Properties;

            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse
                    || !properties.IsXButton1Pressed) {
                return;
            }

            this.ViewModel.TryNavigateOut();
        }
    }
}
