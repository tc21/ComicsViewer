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
        public IEnumerable<string>? VisibleCategories { get; set; }
        public IEnumerable<string>? VisibleAuthors { get; set; }
        public IEnumerable<string>? VisibleTags { get; set; }
    }
}
