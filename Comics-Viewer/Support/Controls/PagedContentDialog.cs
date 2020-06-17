using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Support.Controls {
    public class PagedContentDialog : ContentDialog, IPagedControl {
        public Frame ContentFrame { get; } = new Frame();

        public PagedContentDialog() {
            this.Content = this.ContentFrame;
        }

        public void CloseControl() {
            this.Hide();
        }

        public async Task<ContentDialogResult> NavigateAndShowAsync(Type pageType, object parameter) {
            PagedControlNavigationHelper.Navigate(this, pageType, parameter);
            return await this.ShowAsync();
        }
    }
}
