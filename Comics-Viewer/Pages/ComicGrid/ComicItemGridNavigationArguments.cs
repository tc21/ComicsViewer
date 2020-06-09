using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer {
    public class ComicItemGridNavigationArguments {
        public ComicViewModel ViewModel { get; set; }
        public Action<ComicItemGrid, NavigationEventArgs>? OnNavigatedTo { get; set; }

        public ComicItemGridNavigationArguments(ComicViewModel viewModel, Action<ComicItemGrid, NavigationEventArgs>? onNavigatedTo = null) {
            this.ViewModel = viewModel;
            this.OnNavigatedTo = onNavigatedTo;
        }
    }
}
