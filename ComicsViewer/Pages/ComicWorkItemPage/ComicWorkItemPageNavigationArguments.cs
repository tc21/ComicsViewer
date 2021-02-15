using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class ComicWorkItemPageNavigationArguments {
        public ComicWorkItemPageViewModel ViewModel { get; }

        public ComicWorkItemPageNavigationArguments(ComicWorkItemPageViewModel viewModel) {
            this.ViewModel = viewModel;
        }
    }
}