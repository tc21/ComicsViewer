using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page {
        public SettingsPage() {
            this.InitializeComponent();
        }

        public SettingsPageViewModel? ViewModel;
        private MainViewModel? MainViewModel => ViewModel?.MainViewModel;

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is SettingsPageNavigationArguments args)) {
                throw new ProgrammerError();
            }

            this.ViewModel = new SettingsPageViewModel(args.MainViewModel, args.Profile);
        }

        #region Creating a new profile 

        private async void NewProfileButton_Click(object sender, RoutedEventArgs e) {
            var result = await this.NewProfileDialog.ShowAsync();
            if (result != ContentDialogResult.Primary) {
                return;
            }

            await this.ViewModel!.CreateProfileAsync(this.NewProfileTextBox.Text, 
                copyCurrent: this.NewProfileRadioButtons.SelectedItem == this.NewProfileCopyCurrentProfileRadioButton);
        }

        private void NewProfileTypeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            this.UpdateNewProfileWarningText();
        }

        private void NewProfileTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            this.UpdateNewProfileWarningText();
        }

        private void UpdateNewProfileWarningText() {
            if (this.NewProfileRadioButtons.SelectedIndex == -1) {
                // the default warning should still be shown
                return;
            }

            if (Path.GetInvalidFileNameChars().Any(c => this.NewProfileTextBox.Text.Contains(c))) {
                this.NewProfileWarningTextBlock.Text = "The profile name contains invalid characters.";
                this.NewProfileDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            this.NewProfileDialog.IsPrimaryButtonEnabled = true;
            this.NewProfileWarningTextBlock.Text = "";
        }

        #endregion

        private void AddProfileCategoryButton_Click(object sender, RoutedEventArgs e) {
            this.ViewModel!.AddEmptyProfileCategory();
        }

        private void ProfileCategoryDataGrid_CellEditEnded(object sender, Microsoft.Toolkit.Uwp.UI.Controls.DataGridCellEditEndedEventArgs e) {
            this.SaveProfleCategoriesButton.Visibility = Visibility.Visible;
        }

        private async void SaveProfileCategoriesButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel!.SaveProfileCategoriesAsync();
            this.SaveProfleCategoriesButton.Visibility = Visibility.Collapsed;
        }

        private async void ReloadAllCategoriesButton_Click(object sender, RoutedEventArgs e) {
            var result = await this.ReloadConfirmationContentDialog.ShowAsync();

            if (result != ContentDialogResult.Primary) {
                return;
            }

            await this.MainViewModel!.StartReloadAllComicsTaskAsync();
        }

        private async void ReloadCategoryButton_Click(object sender, RoutedEventArgs e) {
            var result = await this.ReloadConfirmationContentDialog.ShowAsync();

            if (result != ContentDialogResult.Primary) {
                return;
            }

            if (!(((FrameworkElement)sender).DataContext is NamedPath namedPath)) {
                throw new ProgrammerError();
            }

            await this.MainViewModel!.StartReloadCategoryTaskAsync(namedPath);
        }
    }
}
