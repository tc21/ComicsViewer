using System;

#nullable enable

namespace ComicsViewer.Common {
    public class IntendedBehaviorException : Exception {
        public string Title { get; }
        public IntendedBehaviorException(string message, string? title = null) : base(message) {
            title ??= "An operation was unsuccessful";

            this.Title = title;
        }
    }
}
