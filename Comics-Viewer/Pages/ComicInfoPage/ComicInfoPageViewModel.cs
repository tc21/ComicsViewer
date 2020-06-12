using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.Pages {
    public class ComicInfoPageViewModel {
        internal readonly ComicItemGridViewModel ParentViewModel;
        internal readonly ComicItem Item;

        public ComicInfoPageViewModel(ComicItemGridViewModel parentViewModel, ComicItem item) {
            if (item.ItemType != ComicItemType.Work) {
                throw new ApplicationLogicException();
            }

            this.ParentViewModel = parentViewModel;
            this.Item = item;
        }

        public Task OpenComic() {
            return this.ParentViewModel.OpenItemsAsync(new[] { Item });
        }
    }
}
