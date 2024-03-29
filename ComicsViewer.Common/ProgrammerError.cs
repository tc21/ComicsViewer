﻿using System;
using System.Runtime.CompilerServices;

#nullable enable

namespace ComicsViewer.Common {
    /// <summary>
    /// Represents an error caused by programmer error. By design, this should be impossible, but we all make mistakes.
    /// This error should not be handled. It represents a bug that needs to be fixed.
    /// </summary>
    public class ProgrammerError : Exception {
        public ProgrammerError(string message) : base(message) { }
        public ProgrammerError() { }

        public static ProgrammerError Auto(
            [CallerMemberName] string? calledFrom = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int? lineNumber = null
        ) {
            return new ProgrammerError($"{calledFrom} on {filePath}: Line {lineNumber}");
        }

        public static ProgrammerError Unwrapped(
            [CallerMemberName] string? propertyName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int? lineNumber = null
        ) {
            return new ProgrammerError($"found null when unwrapping {propertyName}: on {filePath}: Line {lineNumber}");
        }
    }
}
