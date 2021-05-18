using System.Diagnostics.CodeAnalysis;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

#nullable enable

namespace ComicsViewer.Controls {
    /* Unfortunately since interfaces cannot provide default implementations in .NET Standard 2.0 / .NET Native 2, 
     * we have to duplicate some code when implementing IPagedItemContainer */
    public class PagedFlyout : Flyout, IPagedControl {
        public Frame ContentFrame { get; } = new Frame();

        public PagedFlyout() {
            this.Content = this.ContentFrame;
        }

        public void CloseControl() {
            this.Hide();
        }

        public void NavigateAndShowAt<T, A>([DisallowNull] A parameter, FrameworkElement placementTarget, FlyoutShowOptions? showOptions = null)
        where T: IPagedControlContent<A> {
            PagedControlNavigationHelper.Navigate<T, A>(this, parameter);

            if (showOptions == null) {
                this.ShowAt(placementTarget);
            } else {
                this.ShowAt(placementTarget, showOptions);
            }
        }

        // Flyout specifically names it's Content property as a UIElement instead of object
        object ILikeContentControl.Content => this.Content;
    }
}
