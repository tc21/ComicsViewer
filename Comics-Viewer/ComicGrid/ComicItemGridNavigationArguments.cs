using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer {
    class ComicItemGridNavigationArguments {
        internal Action<ComicItemGrid, NavigationEventArgs>? OnNavigatedTo { get; set; }
        internal ComicViewModel? ViewModel { get; set; }
    }
}
