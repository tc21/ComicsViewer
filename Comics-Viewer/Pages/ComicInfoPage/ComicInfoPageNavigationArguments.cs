using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Pages {
    class ComicInfoPageNavigationArguments {
        public ComicItemGridViewModel ParentViewModel { get; }
        public ComicItem ComicItem { get; }

        public ComicInfoPageNavigationArguments(ComicItemGridViewModel parentViewModel, ComicItem comicItem) {
            this.ParentViewModel = parentViewModel;
            this.ComicItem = comicItem;
        }
    }
}
