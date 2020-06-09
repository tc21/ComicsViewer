using ComicsLibrary;
using ComicsViewer.Filters;
using ComicsViewer.ViewModels;
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
    public sealed partial class FilterPage : Page {
        public FilterPage() {
            this.InitializeComponent();
        }

        public FilterViewModel? ViewModel;

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is FilterPageNavigationArguments args)) {
                throw new ApplicationLogicException("FilterPage must receive a FilterPageNavigationArguments as its navigation argument");
            }

            if (args.Filter == null) {
                throw new ApplicationLogicException("args.Filter cannot be null");
            }

            this.ViewModel = new FilterViewModel(args.Filter!, args.VisibleCategories, args.VisibleAuthors, args.VisibleTags);
        }

        private void ClearCustomFilterButton_Click(object sender, RoutedEventArgs e) {
            this.ViewModel!.GeneratedFilter = null;
        }

        private void ListView_CategorySelectionChanged(object sender, SelectionChangedEventArgs e) {
            var filter = this.ViewModel!.Filter;

            using (filter.DeferNotifications()) {
                foreach (var item in e.AddedItems) {
                    filter.AddCategory(item.ToString());
                }

                foreach (var item in e.RemovedItems) {
                    filter.RemoveCategory(item.ToString());
                }
            }
        }

        private void ListView_AuthorSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var filter = this.ViewModel!.Filter;

            using (filter.DeferNotifications()) {
                foreach (var item in e.AddedItems) {
                    filter.AddAuthor(item.ToString());
                }

                foreach (var item in e.RemovedItems) {
                    filter.RemoveAuthor(item.ToString());
                }
            }
        }

        private void ListView_TagSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var filter = this.ViewModel!.Filter;

            using (filter.DeferNotifications()) {
                foreach (var item in e.AddedItems) {
                    filter.AddTag(item.ToString());
                }

                foreach (var item in e.RemovedItems) {
                    filter.RemoveTag(item.ToString());
                }
            }
        }
    }
}
