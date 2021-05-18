using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ItemPickerDialogContent : IPagedControlContent<ItemPickerDialogNavigationArguments> {
        public ItemPickerDialogContent() {
            this.InitializeComponent();
        }

        private PagedControlAccessor? pagedControlAccessor;
        private Action<ISelectable>? action;
        private ItemPickerDialogProperties? properties;
        private List<ISelectable>? items;

        public PagedControlAccessor PagedControlAccessor { 
            get => pagedControlAccessor ?? throw ProgrammerError.Unwrapped(); 
            private set => pagedControlAccessor = value; 
        }

        private Action<ISelectable> Action { 
            get => action ?? throw ProgrammerError.Unwrapped(); 
            set => action = value; 
        }

        private ItemPickerDialogProperties Properties { 
            get => properties ?? throw ProgrammerError.Unwrapped(); 
            set => properties = value; 
        }
        private List<ISelectable> Items { 
            get => items ?? throw ProgrammerError.Unwrapped();
            set => items = value; 
        }

        public IEnumerable<string> ComboBoxItems => this.Items.Select(item => item.Name);

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            var (accessor, args) = PagedControlAccessor.FromNavigationArguments<ItemPickerDialogNavigationArguments>(
                e.Parameter ?? throw new ProgrammerError("e.Parameter must not be null")
            );
            this.PagedControlAccessor = accessor;
            this.Action = args.Action;
            this.Properties = args.Properties;
            this.Items = args.Items.ToList();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            this.PagedControlAccessor.CloseContainer();
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e) {
            if (this.SelectItemComboBox.SelectedIndex == -1) {
                throw new ProgrammerError();
            }

            this.Action(this.Items[this.SelectItemComboBox.SelectedIndex]);
            this.PagedControlAccessor.CloseContainer();
        }

        private void SelectItemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            this.ActionButton.IsEnabled = true;
        }
    }
}
