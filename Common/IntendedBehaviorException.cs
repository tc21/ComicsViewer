using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Common {
    internal class IntendedBehaviorException : Exception {
        public string Title { get; }
        public IntendedBehaviorException(string message, string? title = null) : base(message) {
            title ??= "An operation was unsuccessful";

            this.Title = title;
        }
    }
}
