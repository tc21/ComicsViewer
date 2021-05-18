using System.Diagnostics.CodeAnalysis;
using ComicsViewer.Common;
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

    public interface IPagedControlContent<TNavigationArgument> {
        public PagedControlAccessor? PagedControlAccessor { get; }
    }

    public class PagedControlNavigationArguments {
        public readonly IPagedControl Container;
        public readonly object Parameter;

        public PagedControlNavigationArguments(IPagedControl container, object parameter) {
            this.Parameter = parameter;
            this.Container = container;
        }
    }

    public static class PagedControlNavigationHelper {
        public static void Navigate<T, A>(IPagedControl container, [DisallowNull] A parameter) where T: IPagedControlContent<A> {
            if (container.Content != container.ContentFrame) {
                throw new ProgrammerError("IPagedItemContainer cannot have custom content");
            }

            _ = container.ContentFrame.NavigateToType(
                typeof(T),
                new PagedControlNavigationArguments(container, parameter),
                new FrameNavigationOptions { IsNavigationStackEnabled = false }
            );
        }
    }

    public class PagedControlAccessor {
        private readonly IPagedControl container;

        private PagedControlAccessor(IPagedControl container) {
            this.container = container;
        }


        public static (PagedControlAccessor, TArgs) FromNavigationArguments<TArgs>(object navigationArguments) {
            if (navigationArguments is not PagedControlNavigationArguments args) {
                throw new ProgrammerError("FromNavigationArguments must receive an argument of type PagedItemNavigationArguments as its argument");
            }

            if (args.Parameter is not TArgs param) {
                throw new ProgrammerError("PagedItemNavigationArguments.Parameter was of incorrect type");
            }

            return (new PagedControlAccessor(args.Container), param);
        }

        public void CloseContainer() => this.container.CloseControl();
    }
}
