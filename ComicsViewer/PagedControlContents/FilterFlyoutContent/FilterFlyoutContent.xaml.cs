using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class FilterFlyoutContent : Page, IPagedControlContent {
        public FilterFlyoutContent() {
            this.InitializeComponent();
        }

        public FilterFlyoutViewModel? ViewModel;
        public PagedControlAccessor? PagedControlAccessor => throw new NotImplementedException();

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            var (_, args) = PagedControlAccessor.FromNavigationArguments<FilterFlyoutNavigationArguments>(e.Parameter);

            if (args.Filter == null || args.AuxiliaryInfo == null || args.ParentViewModel == null) {
                throw new ProgrammerError("args cannot be null");
            }

            this.ViewModel = new FilterFlyoutViewModel(args.ParentViewModel, args.Filter, args.AuxiliaryInfo);

            /* Note: SelectedItems is currently being set BEFORE ItemsSource, and our solution of allowing it to be
             * done is more of a hack than the proper way to handle it. One day we might decide it to be better to set
             * ItemsSource manually in code in these lines, before setting SelectedItems. */
            this.CategoryChecklist.SelectedItems = this.ViewModel.Categories.Where(e => this.ViewModel.Filter.ContainsCategory(e.Name));
            this.AuthorChecklist.SelectedItems = this.ViewModel.Authors.Where(e => this.ViewModel.Filter.ContainsAuthor(e.Name));
            this.TagChecklist.SelectedItems = this.ViewModel.Tags.Where(e => this.ViewModel.Filter.ContainsTag(e.Name));

            /* Another note: setting SelectedItems trigger a SelectedItemsChanged event, which we don't want to happen
             * until after they're initialized */
            this.CategoryChecklist.SelectedItemsChanged += this.CategoryChecklist_SelectedItemsChanged;
            this.AuthorChecklist.SelectedItemsChanged += this.AuthorChecklist_SelectedItemsChanged;
            this.TagChecklist.SelectedItemsChanged += this.TagChecklist_SelectedItemsChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e) {
            this.CategoryChecklist.SelectedItemsChanged -= this.CategoryChecklist_SelectedItemsChanged;
            this.AuthorChecklist.SelectedItemsChanged -= this.AuthorChecklist_SelectedItemsChanged;
            this.TagChecklist.SelectedItemsChanged -= this.TagChecklist_SelectedItemsChanged;
        }

        private void ClearCustomFilterButton_Click(object sender, RoutedEventArgs e) {
            this.ViewModel!.GeneratedFilter = null;
        }

        private void CategoryChecklist_SelectedItemsChanged(ExpandableChecklist sender, SelectedItemsChangedEventArgs e) {
            var filter = this.ViewModel!.Filter;
            this.HandleSelectionChange(e, filter, filter.AddCategory, filter.RemoveCategory);
        }

        private void AuthorChecklist_SelectedItemsChanged(ExpandableChecklist sender, SelectedItemsChangedEventArgs e) {
            var filter = this.ViewModel!.Filter;
            this.HandleSelectionChange(e, filter, filter.AddAuthor, filter.RemoveAuthor);
        }

        private void TagChecklist_SelectedItemsChanged(ExpandableChecklist sender, SelectedItemsChangedEventArgs e) {
            var filter = this.ViewModel!.Filter;
            this.HandleSelectionChange(e, filter, filter.AddTag, filter.RemoveTag);
        }

        private void HandleSelectionChange(SelectedItemsChangedEventArgs e, Filter filter, Func<string, bool> addItem, Func<string, bool> removeItem) {
            using (filter.DeferNotifications()) {
                foreach (var item in e.RemovedItems) {
                    removeItem(item.Name);
                }

                foreach (var item in e.AddedItems) {
                    addItem(item.Name);
                }
            }
        }

        /* Limits the size of the filter flyout by allowing only one checklist to be open at a time */
        private void Checklist_Expanding(object sender, EventArgs e) {
            foreach (var checklist in new[] { this.CategoryChecklist, this.AuthorChecklist, this.TagChecklist }) {
                if (checklist != sender) {
                    checklist.IsExpanded = false;
                }
            }
        }
    }
}
