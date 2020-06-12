using ComicsViewer.Pages.Helpers;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Filters {
    public class FilterPageNavigationArguments {
        public Filter? Filter { get; set; }
        public FilterViewAuxiliaryInfo? AuxiliaryInfo { get; set; }
    }
}
