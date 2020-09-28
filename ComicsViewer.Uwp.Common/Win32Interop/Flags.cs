
using DWORD = System.UInt32;

#nullable enable

namespace ComicsViewer.Uwp.Common.Win32Interop {
    internal static class Flags {
        public enum FormatMessage : DWORD {
            AllocateBuffer = 0x00000100,
            FromSystem = 0x00001000,
            IgnoreInserts = 0x00000200,
        }
    }

    internal enum FINDEX_INFO_LEVELS {
        FindExInfoStandard = 0,
        FindExInfoBasic = 1
    }

    internal enum FINDEX_SEARCH_OPS {
        FindExSearchNameMatch = 0,
        FindExSearchLimitToDirectories = 1,
        FindExSearchLimitToDevices = 2
    }

    internal enum GET_FILEEX_INFO_LEVELS {
        GetFileExInfoStandard = 0,
        GetFileExMaxInfoLevel = 1
    }
}
