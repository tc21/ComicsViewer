using System.Runtime.InteropServices;

using DWORD = System.UInt32;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

#nullable enable

namespace ComicsViewer.Uwp.Common.Win32Interop {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct WIN32_FIND_DATA {
        public DWORD dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public DWORD nFileSizeHigh;
        public DWORD nFileSizeLow;
        public DWORD dwReserved0;
        public DWORD dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct WIN32_FILE_ATTRIBUTE_DATA {
        public DWORD dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public DWORD nFileSizeHigh;
        public DWORD nFileSizeLow;
    }
}
