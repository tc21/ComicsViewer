using System;
using System.Collections.Generic;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ComicsViewer.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ComicInfoPage : Page {
        public ComicInfoPage() {
            this.InitializeComponent();
        }

        private ComicInfoPageViewModel ViewModel;

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is ComicInfoPageNavigationArguments args)) {
                throw new ApplicationLogicException();
            }

            this.ViewModel = new ComicInfoPageViewModel(args.ParentViewModel, args.ComicItem);
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel.OpenComic();
        }
    }
}
