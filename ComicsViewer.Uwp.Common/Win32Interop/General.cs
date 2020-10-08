using System;
using System.IO;
using System.Runtime.InteropServices;

using ComicsViewer.Common;

#nullable enable

namespace ComicsViewer.Uwp.Common.Win32Interop {
    internal static class Win32ReturnValues {
        public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
    }

    internal static class General {
        [DllImport("api-ms-win-core-errorhandling-l1-1-1.dll")]
        internal static extern uint GetLastError();

        [DllImport("api-ms-win-core-localization-l1-2-1.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint FormatMessage(
            Flags.FormatMessage dwFlags,
            IntPtr lpSource,
            uint dwMessageId,
            uint dwLanguageId,
            out string lpBuffer,
            uint nSize,
            IntPtr arguments
        );

        public static Exception ThrowLastError(string? additionalInfo = null, bool makeCsharpExceptions = true) {
            var error = GetLastError();
            if (error == 0) {
                throw new Exception("ThrowLastError called when there is no error");
            }

            var outputLength = FormatMessage(
                Flags.FormatMessage.AllocateBuffer | Flags.FormatMessage.FromSystem | Flags.FormatMessage.IgnoreInserts,
                IntPtr.Zero,
                error,
                0,
                out var message,
                0,
                IntPtr.Zero
            );

            if (outputLength == 0) {
                throw new ProgrammerError(
                    $"When calling ThrowLastError: FormatMessage indicated an error with error code {GetLastError()}");
            }

            message = message.Trim();

            if (!makeCsharpExceptions) {
                return new NativeException((int) error, message, additionalInfo);
            }

            Exception? exception = error switch {
                var e when e == 2 || e == 3
                    => additionalInfo == null ? new FileNotFoundException(message) : new FileNotFoundException(message, additionalInfo),
                5 => new UnauthorizedAccessException(message),
                _ => null
            };

            if (exception != null) {
                return exception;
            }

            return new NativeException((int)error, message, additionalInfo);
        }
    }
}
