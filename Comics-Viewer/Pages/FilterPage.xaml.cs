﻿using ComicsLibrary;
using ComicsViewer.Filters;
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
            if (!(e.Parameter is Filter filter)) {
                throw new ApplicationLogicException("FilterPage must receive a filter as its navigation argument");
            }

            this.ViewModel = new FilterViewModel(filter);
        }

        private void ClearCustomFilterButton_Click(object sender, RoutedEventArgs e) {
            this.ViewModel!.Filter.GeneratedFilter = null;
        }
    }
}
