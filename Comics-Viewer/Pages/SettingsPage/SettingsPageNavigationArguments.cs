using ComicsViewer.Features;
using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class SettingsPageNavigationArguments {
        public MainViewModel MainViewModel { get; }
        public UserProfile Profile { get; }

        public SettingsPageNavigationArguments(MainViewModel mainViewModel, UserProfile profile) {
            this.MainViewModel = mainViewModel;
            this.Profile = profile;
        }
    }
}
