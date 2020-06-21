using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;

#nullable enable

namespace ComicsViewer.Pages {
    public class FilterFlyoutNavigationArguments {
        public Filter? Filter { get; set; }
        public ComicItemGridViewModel? ParentViewModel { get; set; }
        public FilterViewAuxiliaryInfo? AuxiliaryInfo { get; set; }
    }
}
