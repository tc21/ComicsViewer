using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer {
    /// <summary>
    /// Represents an error caused by programmer error. By design, this should be impossible, but we all make mistakes.
    /// This error should not be handled. It represents a bug that needs to be fixed.
    /// </summary>
    internal class ProgrammerError : Exception {
        public ProgrammerError(string message) : base(message) { }
        public ProgrammerError() : base() { }

        public static ProgrammerError Auto(
            [CallerMemberName] string? calledFrom = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int? lineNumber = null
        ) {
            return new ProgrammerError($"{calledFrom} on {filePath}: Line {lineNumber}");
        }
    }
}
