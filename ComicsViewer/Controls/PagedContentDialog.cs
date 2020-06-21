using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Controls {
    public class PagedContentDialog : ContentDialog, IPagedControl {
        public Frame ContentFrame { get; } = new Frame();

        public PagedContentDialog() : base() {
            this.Content = this.ContentFrame;
            /* For some reason this subclassing breaks its corner radius. Is this related to the same bug that
             * prevents ContentDialogMaxWidth from working? That said setting MaxWidth here doesn't work. */
            this.CornerRadius = new Windows.UI.Xaml.CornerRadius(4);
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
