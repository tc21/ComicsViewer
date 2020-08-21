using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.Support {
    internal class IntendedBehaviorException : Exception {
        public string Title { get; }
        public IntendedBehaviorException(string message, string title = "An operation was unsuccessful") : base(message) {
            this.Title = title;
        }
    }
}
