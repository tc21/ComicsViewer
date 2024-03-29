﻿#nullable enable

namespace ComicsViewer.Support {
    public class Commented<T> {
        public readonly T Value;
        public readonly string? Comment;

        protected Commented(T value, string? comment) {
            this.Value = value;
            this.Comment = comment;
        }
    }

    /// <summary>
    /// ValidateResult promises that there will be a comment if it is Err.
    /// Part of why we trust it to work is that C# only allows bools as a if/loop condition.
    /// </summary>
    public class ValidateResult : Commented<bool> {
        private ValidateResult(bool value, string? comment) : base(value, comment) { }
        public static ValidateResult Ok(string? comment = null) => new(true, comment);
        public static ValidateResult Err(string comment = "This value is invalid.") => new(false, comment);

        public bool IsOk => this.Value;
        public bool IsErr => !this.IsOk;

        public static implicit operator ValidateResult(bool value) {
            if (value) {
                return Ok();
            }

            return Err();
        }

        /// <summary>
        /// If you return a string for whatever reason, we assume it's an error
        /// </summary>
        public static implicit operator ValidateResult(string value) => Err(value);

        public static implicit operator bool(ValidateResult result) => result.Value;

        public ValidateResult CombineWith(ValidateResult rhs) {
            if (this.Value == rhs.Value) {
                return new ValidateResult(
                    value: this.Value && rhs.Value,
                    comment: this.Comment ?? rhs.Comment
                );
            } else if (this.IsErr) {
                return this;
            } else {
                return rhs;
            }
        }
    }
}
