using System;
using System.IO;
using System.Runtime.InteropServices;

using DWORD = System.UInt32;
using ComicsViewer.Common;

#nullable enable

namespace ComicsViewer.Uwp.Common.Win32Interop {
    internal static class Win32ReturnValues {
        public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
    }

    internal static class General {
        [DllImport("api-ms-win-core-errorhandling-l1-1-1.dll")]
        internal static extern DWORD GetLastError();

        [DllImport("api-ms-win-core-localization-l1-2-1.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern DWORD FormatMessage(
            Flags.FormatMessage dwFlags,
            IntPtr lpSource,
            DWORD dwMessageId,
            DWORD dwLanguageId,
            out string lpBuffer,
            DWORD nSize,
            IntPtr Arguments
        );

        public static Exception ThrowLastError(string? additionalInfo = null, bool makeCsharpExceptions = true) {
            var error = GetLastError();
            if (error == 0) {
                throw new Exception("ThrowLastError called when there is no error");
            }

            var output_length = FormatMessage(
                Flags.FormatMessage.AllocateBuffer | Flags.FormatMessage.FromSystem | Flags.FormatMessage.IgnoreInserts,
                IntPtr.Zero,
                error,
                0,
                out var message,
                0,
                IntPtr.Zero
            );

            if (output_length == 0) {
                throw new ProgrammerError(
                    $"When calling ThrowLastError: FormatMessage indicated an error with error code {GetLastError()}");
            }

            message = message.Trim();

            if (makeCsharpExceptions) {
                Exception? cserr = error switch
                {
                    var e when (e == 2 || e == 3)
                        => (additionalInfo == null) ? new FileNotFoundException(message) : new FileNotFoundException(message, additionalInfo),
                    5 => new UnauthorizedAccessException(message),
                    _ => null
                };

                if (cserr != null) {
                    return cserr;
                }
            }

            return new NativeException((int)error, message, additionalInfo);
        }
    }
}
