using ComicsViewer.Common;
using Windows.UI.Xaml.Navigation;

namespace MusicPlayer {
    public sealed partial class InfoPage {
        public InfoPage() {
            this.InitializeComponent();
        }

        private ViewModel _mainViewModel;
        public ViewModel MainViewModel => this._mainViewModel ?? throw ProgrammerError.Unwrapped();

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is not ViewModel vm) {
                throw ProgrammerError.Auto();
            }

            this._mainViewModel = vm;
        }
    }
}
