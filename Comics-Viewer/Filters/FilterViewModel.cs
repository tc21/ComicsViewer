using ComicsLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Filters {
    public class FilterViewModel : ViewModels.ViewModelBase {
        public bool GeneratedFilterEnabled => this.Filter.GeneratedFilter != null;
        public string GeneratedFilterDescription => $"Automatically generated filter ({this.Filter.Metadata.GeneratedFilterItemCount} items)";

        public Filter Filter { get; }

        public FilterViewModel(Filter filter) {
            this.Filter = filter;
            this.Filter.FilterChanged += this.Filter_FilterChanged;
        }

        private void Filter_FilterChanged(Filter filter) {
            this.OnPropertyChanged("");
        }
    }
}
