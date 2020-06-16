using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ComicsViewer.Profiles;
using ComicsLibrary;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using ComicsViewer.Pages;
using ComicsViewer.Filters;
using MUXC = Microsoft.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer {
    /* Note: MainPage does not have its own view model because how simple its logic is. In the future when we complicate 
     * its logic, we may want a separate view model class. (This also means we are allowed to communicate a little with 
     * the models here) */
    public sealed partial class MainPage : Page {
        //private ComicStore comicStore = ComicStore.EmptyComicStore;
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

        // Public because it's used in the xaml
        public readonly MainViewModel ViewModel = new MainViewModel();

        #region Navigation

        private async void NavigationView_Loaded(object sender, RoutedEventArgs e) {
            /* Note: The app currently doesn't support multiple pages, but it one day might. */
            if (!ProfileManager.Initialized) {
                await ProfileManager.InitializeAsync();
            }

            this.ViewModel.ProfileChanged += this.ViewModel_ProfileChanged;
            this.ViewModel.NavigationRequested += this.ViewModel_NavigationRequested;

            // Initialize view models and fire events for the first time
            await this.ViewModel.SetDefaultProfile();

            // We're probably not supposed to directly access the filter but whatever
            this.ViewModel.Filter.FilterChanged += this.Filter_FilterChanged;
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
            this.ProfileNavigationViewItem.Content = e.NewProile.Name;
            this.ProfileNavigationViewItem.MenuItems.Clear();
            foreach (var existingProfile in ProfileManager.LoadedProfiles) {
                if (existingProfile != e.NewProile.Name) {
                    this.ProfileNavigationViewItem.MenuItems.Add(existingProfile);
                }
            }

            this.NavigationView.SelectedItem = this.ProfileNavigationViewItem;

            // This will fire ViewModel.NavigationRequested. 
            // ignoreCache: true bypasses the default behavior of scrolling up to the top when "reloading"
            this.ViewModel.Navigate(MainViewModel.DefaultNavigationTag, ignoreCache: true);
        }

        private void ViewModel_NavigationRequested(MainViewModel sender, NavigationRequestedEventArgs e) {
            switch (e.NavigationType) {
                case NavigationType.Back:
                    if (e.Tag == null) {
                        throw new ApplicationLogicException();
                    }

                    if (!this.ContentFrame.CanGoBack) {
                        // It turns out this is actually possible for some unknown reason. It's pretty much a bug, but
                        // I can't figure out why, and doing this works too:
                        // Note: most likely it's because two separate viewModels are calling NavigateOut twice. It works because
                        // There's enough pages in the buffer and caching is enabled. The question is: why is a top level
                        // view modle able to call NavigateOut?
                        this.ViewModel.Navigate(e.Tag!);
                        return;
                    }

                    this.ContentFrame.GoBack();

                    break;
                case NavigationType.Scroll:
                    this.activeContent?.ScrollToTop();
                    break;
                case NavigationType.New:
                    if (e.PageType == null || e.Comics == null) {
                        throw new ApplicationLogicException();
                    }

                    var navigationArguments = new ComicItemGridNavigationArguments {
                        ViewModel = new ComicItemGridViewModel(sender, e.Comics),
                        OnNavigatedTo = (grid, e) => this.activeContent = grid
                    };

                    this.ContentFrame.Navigate(e.PageType, navigationArguments, e.TransitionInfo);

                    this.currentView.AppViewBackButtonVisibility =
                        (sender.NavigationLevel > 0) ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Disabled;
                    break;
                default:
                    throw new ApplicationLogicException($"Unhandled NavigationType '{e.NavigationType}'.");
            }

        }

        private async void NavigationView_ItemInvoked(MUXC.NavigationView sender, MUXC.NavigationViewItemInvokedEventArgs args) {
            if (args.InvokedItem == null) {
                // Don't know why this happens yet, but it happens when you select "continue running task" and then switch profiles after that.
                return;
            }

            if (args.IsSettingsInvoked) {
                // Don't navigate to settings twice
                if (!(this.ContentFrame.Content is SettingsPage)) {
                    this.ViewModel.NavigationLevel = 2;
                    this.currentView.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    this.ContentFrame.Navigate(typeof(SettingsPage), new SettingsPageNavigationArguments(this.ViewModel!, this.ViewModel!.Profile));
                }
                return;
            }
            
            /* There are two types of navigation view items that can be invoked:
             * 1. A "profile switch" item: Tag = null, Content = <profile name>
             * 2. A "navigate" item: Tag = <page type>
             * 
             * I don't like it cause it seems hacky, but whatever for now
             */
            if (args.InvokedItemContainer.Tag == null) {
                var profileName = args.InvokedItemContainer.Content.ToString();
                await this.ViewModel.SetProfileAsync(profileName);
                return;
            }

            var tag = args.InvokedItemContainer.Tag.ToString();
            this.ViewModel.Navigate(tag, args.RecommendedNavigationTransitionInfo);
        }

        private void CurrentView_BackRequested(object sender, BackRequestedEventArgs e) {
            this.ViewModel.NavigateOut();
        }

        private void Frame_NavigationFailed(object _, NavigationFailedEventArgs e) {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        #endregion

        #region Search


        private void Filter_FilterChanged(Filter filter) { 
            if (filter.IsActive) {
                this.FilterButton.Background = this.Resources["SystemControlAccentAcrylicElementAccentMediumHighBrush"] as Brush;
            } else {
                this.FilterButton.Background = null;
            }
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) {
                var suggestions = Search.GetSearchSuggestions(sender.Text).ToList();
                while (suggestions.Count > 4) {
                    suggestions.RemoveAt(4);
                }

                sender.ItemsSource = suggestions;
            }
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) {
            sender.Text = (string)args.SelectedItem;
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) {
            if (Search.Compile(sender.Text) is Func<Comic, bool> search) {
                this.ViewModel.SubmitSearch(search, sender.Text);

                // remove focus from the search box (partially to indicate that the search succeeded)
                this.activeContent?.Focus(FocusState.Pointer);
            }
        }


        private void AutoSuggestBox_GotFocus(object sender, RoutedEventArgs e) {
            var suggestions = Search.GetSearchSuggestions(this.SearchBox.Text).ToList();
            while (suggestions.Count > 4) {
                suggestions.RemoveAt(4);
            }

            this.SearchBox.ItemsSource = suggestions;
            this.SearchBox.IsSuggestionListOpen = true;
        }

        #endregion

        private void FilterNavigationViewItem_Tapped(object sender, TappedRoutedEventArgs e) {
            if (this.activeContent == null) {
                // The app isn't ready yet
                return;
            }

            var flyout = (this.Resources["FilterFlyout"] as Flyout)!;
            this.FilterFlyoutFrame.Navigate(typeof(FilterPage), this.ViewModel.GetFilterPageNavigationArguments(this.activeContent.ViewModel!));
            flyout.ShowAt(sender as FrameworkElement);
        }

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
    }
}
