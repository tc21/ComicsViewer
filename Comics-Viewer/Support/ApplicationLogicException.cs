using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer {
    /// <summary>
    /// Represents an error caused by programmer error. By design, this should be impossible, but we all make mistakes.
    /// This error should not be handled. It represents a bug that needs to be fixed.
    /// </summary>
    internal class ApplicationLogicException : Exception {
        public ApplicationLogicException(string message) : base(message) { }
        public ApplicationLogicException() : base() { }
    }
}
