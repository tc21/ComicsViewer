using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Support;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public sealed partial class TextInputDialogContent : IPagedControlContent<TextInputDialogNavigationArguments> {
        public TextInputDialogContent() {
            this.InitializeComponent();
        }

        private PagedControlAccessor? pagedControlAccessor;
        private TextInputDialogProperties? properties;

        public PagedControlAccessor PagedControlAccessor {
            get => pagedControlAccessor ?? throw ProgrammerError.Unwrapped();
            private set => pagedControlAccessor = value;
        }

        private TextInputDialogProperties Properties {
            get => properties ?? throw ProgrammerError.Unwrapped();
            set => properties = value;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            var (controller, args) =
                PagedControlAccessor.FromNavigationArguments<TextInputDialogNavigationArguments>(
                    e.Parameter ?? throw new ProgrammerError("e.Parameter must not be null")
                );

            this.PagedControlAccessor = controller;
            this.Properties = args.Properties;

            this.EditItemTitleTextBox.RegisterHandlers(
                get: () => args.InitialValue,
                saveAsync: async value => {
                    if (args.Properties.StripWhitespace) {
                        value = value.Trim();
                    }

                    await args.AsyncAction(value);
                    this.PagedControlAccessor.CloseContainer();
                },
                validate: value => {
                    if (args.Properties.StripWhitespace) {
                        value = value.Trim();
                    }

                    return args.Validate?.Invoke(value) ?? ValidateResult.Ok();
                },
                canAlreadySubmit: args.Properties.CanInitiallySubmit
            );
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            this.PagedControlAccessor.CloseContainer();
        }
    }
}
