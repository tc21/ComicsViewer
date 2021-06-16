using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer.Controls {
    public class PagedContentDialog : ContentDialog, IPagedControl {
        public Frame ContentFrame { get; } = new();

        public PagedContentDialog() {
            this.Content = this.ContentFrame;
            /* For some reason this subclassing breaks its corner radius. Is this related to the same bug that
             * prevents ContentDialogMaxWidth from working? That said setting MaxWidth here doesn't work. */
            this.CornerRadius = new Windows.UI.Xaml.CornerRadius(4);
        }

        public void CloseControl() {
            this.Hide();
        }

        public async Task<ContentDialogResult> NavigateAndShowAsync<T, TArgs>([DisallowNull] TArgs parameter) where T: IPagedControlContent<TArgs> {
            PagedControlNavigationHelper.Navigate<T, TArgs>(this, parameter);
            return await this.ShowAsync();
        }
    }
}
