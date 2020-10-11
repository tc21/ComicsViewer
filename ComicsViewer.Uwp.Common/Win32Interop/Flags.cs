using System;
using DWORD = System.UInt32;

#nullable enable

namespace ComicsViewer.Uwp.Common.Win32Interop {
    internal static class Flags {
        [Flags]
        public enum FormatMessage : DWORD {
            AllocateBuffer = 0x00000100,
            FromSystem = 0x00001000,
            IgnoreInserts = 0x00000200,
        }

        public enum FindExInfoLevel {
            Standard = 0,
            Basic = 1
        }

        public enum FindExSearchOp {
            NameMatch = 0,
            LimitToDirectories = 1,
            LimitToDevices = 2
        }

        public enum GetFileExInfoLevel {
            Standard = 0,
            Max = 1
        }
    }
}
