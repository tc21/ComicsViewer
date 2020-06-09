using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer {
    public class ComicItemGridNavigationArguments {
        public ComicItemGridViewModel? ViewModel { get; set; }
        public Action<ComicItemGrid, NavigationEventArgs>? OnNavigatedTo { get; set; }
    }
}
