using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.Support {
    internal class IntendedBehaviorException : Exception {
        public IntendedBehaviorException(string message) : base(message) { }
    }
}
