using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Controls {
    /* 1. Implement IPagedControl (an example has been given in PagedContentDialog)
     * 2. Implement navigation methods using PagedControlNavigationHelper.Navigate. Without them you won't be able to
     *    actually navigate to your control (again, see PagedContentDialog);
     * 3. Implement IPagedControlContent by looking at OnNavigatedTo(NavigationEventArgs e),
     *      replacing:
     *          var args = (ArgumentType)e.Parameter;
     *      with:
     *          var (accessor, args) = PagedControlAccessor.FromNavigationArguments<ArgumentType>(e.Parameter);
     *          this.PagedControlAccessor = accessor;
     * 4. Replace your controls and method calls
     *      replacing:
     *          .xaml: <Control> <Frame /> </Control>
     *          .xaml.cs: Frame.Navigate(...); 
     *                    Control.ShowAsync()
     *      with:
     *          .xaml: <PagedControl />
     *          .xaml.cs: PagedControl.NavigateAndShowAsync(...);
     *          
     * Note: IPagedControlContent is not actually required, but you should use it as a sanity check, so you don't forget
     *       any of these steps.
     */

    public interface ILikeContentControl {
        public object Content { get; }
    }

    public interface IPagedControl : ILikeContentControl {
        public Frame ContentFrame { get; }
        public void CloseControl();
    }

    public interface IPagedControlContent {
        public PagedControlAccessor? PagedControlAccessor { get; }
    }

    public class PagedControlNavigationArguments {
        public IPagedControl Container;
        public object Parameter;

        public PagedControlNavigationArguments(IPagedControl container, object parameter) {
            this.Parameter = parameter;
            this.Container = container;
        }
    }

    public static class PagedControlNavigationHelper {
        public static void Navigate(IPagedControl container, Type pageType, object parameter) {
            if (!typeof(IPagedControlContent).IsAssignableFrom(pageType)) {
                throw new ProgrammerError("An argument to IPagedItemContainer.NavigateTo must conform to IPagedItemContent");
            }

            if (container.Content != container.ContentFrame) {
                throw new ProgrammerError("IPagedItemContainer cannot have custom content");
            }

            _ = container.ContentFrame.NavigateToType(
                pageType,
                new PagedControlNavigationArguments(container, parameter),
                new FrameNavigationOptions { IsNavigationStackEnabled = false }
            );
        }
    }

    public class PagedControlAccessor {
        private readonly IPagedControl container;

        public PagedControlAccessor(IPagedControl container) {
            this.container = container;
        }

        public static (PagedControlAccessor, T) FromNavigationArguments<T>(object navigationArguments) {
            if (!(navigationArguments is PagedControlNavigationArguments args)) {
                throw new ProgrammerError("FromNavigationArguments must receive an argument of type PagedItemNavigationArguments as its argument");
            }

            if (!(args.Parameter is T param)) {
                throw new ProgrammerError("PagedItemNavigationArguments.Parameter was of incorrect type");
            }

            return (new PagedControlAccessor(args.Container), param);
        }

        public void CloseContainer() => this.container.CloseControl();
    }
}
