﻿using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace ComicsLibrary {
    /// <summary>
    /// Represents an error caused by programmer error. By design, this should be impossible, but we all make mistakes.
    /// This error should not be handled. It represents a bug that needs to be fixed.
    /// </summary>
    internal class ProgrammerError : Exception {
        public ProgrammerError(string message) : base(message) { }
        public ProgrammerError() : base() { }
    }
}