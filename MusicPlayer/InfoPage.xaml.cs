using ComicsViewer.Common;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace MusicPlayer {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class InfoPage {
        public InfoPage() {
            this.InitializeComponent();
        }

        private ViewModel _mainViewModel;
        public ViewModel MainViewModel => this._mainViewModel ?? throw new ProgrammerError("auto unwrapped property not set!");

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is ViewModel vm)) {
                throw ProgrammerError.Auto();
            }

            this._mainViewModel = vm;
        }
    }
}
