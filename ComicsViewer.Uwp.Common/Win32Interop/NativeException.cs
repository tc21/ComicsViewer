using System;

#nullable enable

namespace ComicsViewer.Uwp.Common.Win32Interop {
    public class NativeException : Exception {
        public int ErrorCode { get; }

        public NativeException() { }
        public NativeException(int errorCode, string? message = null, string? additionalInfo = null) : base(MakeMessage(errorCode, message, additionalInfo)) {
            this.ErrorCode = errorCode;
        }

        public override string ToString() {
            return base.ToString();
        }

        private static string MakeMessage(int errorCode, string? message, string? additionalInfo) {
            var result = $"{errorCode}";

            if (message is string m) {
                result += $": {m}";
            }

            if (additionalInfo is string s) {
                result += $"({s})";
            }

            return result;
        }
    }
}
