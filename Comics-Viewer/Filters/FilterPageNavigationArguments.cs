using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.Filters {
    class FilterPageNavigationArguments {
        public ComicViewModel ViewModel { get; set; }
        public Filter Filter { get; set; }

        public FilterPageNavigationArguments(ComicViewModel viewModel, Filter filter) {
            this.ViewModel = viewModel;
            this.Filter = filter;
        }
    }
}
