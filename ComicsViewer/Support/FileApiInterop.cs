using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using DWORD = System.UInt32;
using WORD = System.UInt16;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

#nullable enable

namespace ComicsViewer.Support.Interop {

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

    public static class FileApiInterop {
        private enum FormatMessageFlags : DWORD {
            AllocateBuffer = 0x00000100,
            FromSystem = 0x00001000,
            IgnoreInserts = 0x00000200,
        }

        [DllImport("api-ms-win-core-errorhandling-l1-1-1.dll")]
        private static extern DWORD GetLastError();

        [DllImport("api-ms-win-core-localization-l1-2-1.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern DWORD FormatMessage(
            FormatMessageFlags dwFlags,
            IntPtr lpSource,
            DWORD dwMessageId,
            DWORD dwLanguageId,
            out string lpBuffer,
            DWORD nSize,
            IntPtr Arguments
        );

        private static NativeException ThrowLastError(string? additionalInfo = null) {
            var error = GetLastError();
            if (error == 0) {
                throw new Exception("ThrowLastError called when there is no error");
            }

            var output_length = FormatMessage(
                FormatMessageFlags.AllocateBuffer | FormatMessageFlags.FromSystem | FormatMessageFlags.IgnoreInserts,
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

            return new NativeException((int)error, message, additionalInfo);
        }

        #region FindFirstFileEx

        private enum FINDEX_INFO_LEVELS {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }

        private enum FINDEX_SEARCH_OPS {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WIN32_FIND_DATA {
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

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstFileExFromApp(
            string lpFileName,
            FINDEX_INFO_LEVELS fInfoLevelId,
            out WIN32_FIND_DATA lpFindFileData,
            FINDEX_SEARCH_OPS fSearchOp,
            IntPtr lpSearchFilter,
            DWORD dwAdditionalFlags
        );

        [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("api-ms-win-core-file-l1-1-0.dll")]
        private static extern bool FindClose(IntPtr hFindFile);

        #endregion

        #region GetFileAttributesEx

        private enum GET_FILEEX_INFO_LEVELS {
            GetFileExInfoStandard = 0,
            GetFileExMaxInfoLevel = 1
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WIN32_FILE_ATTRIBUTE_DATA {
            public DWORD dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public DWORD nFileSizeHigh;
            public DWORD nFileSizeLow;
        }

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetFileAttributesExFromApp(
            string lpFileName,
            GET_FILEEX_INFO_LEVELS fInfoLevelId,
            out WIN32_FILE_ATTRIBUTE_DATA lpFileInformation
        );

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateDirectoryFromApp(
            string lpPathName,
            [Optional]
            IntPtr lpSecurityAttributes
        );

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileFromApp(
            string lpExistingFileName,
            string lpNewFileName
        );


        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CopyFileFromApp(
            string lpExistingFileName,
            string lpNewFileName,
            bool bFailIfExists
        );

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveDirectoryFromApp(string lpPathName);

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFileFromApp(string lpPathName);

        #endregion

        private static class Win32ReturnValues {
            public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        }

        private static class FileAttribute {
            public const int Directory = 16;
        }

        private static (string dirname, string basename) SplitPath(string path) {
            var child = Path.GetFileName(path);
            var parent = Path.GetDirectoryName(path);

            if (child == "") {
                child = Path.GetFileName(parent);
                parent = Path.GetDirectoryName(parent);
            }

            return (parent, child);
        }

        /* General comments: The main purpose of this class is to do something about the StorageFile API's abysmal file 
         * copy performance. The StorageFile API's file listing performance is actually pretty good, so there's no need
         * to go about replacing all the nice and tidy GetFilesAsync with these methods, which actually would block the
         * UI thread unless wrapped in Task.Run() */

        public static void MoveDirectory(string path, string newName, bool createIntermediateDirectories = true) {
            if (!Path.IsPathRooted(path) || !Path.IsPathRooted(newName)) {
                throw new ArgumentException("MoveFile must receive rooted paths as arguments.");
            }

            if (path.EndsWith("\\") || path.EndsWith("/") || newName.EndsWith("\\") || newName.EndsWith("/")) {
                throw new ProgrammerError("Argument must not end in a slash");
            }

            if (createIntermediateDirectories) {
                CreateDirectories(SplitPath(newName).dirname);
            }

            if (path[0] == newName[0]) {
                if (!MoveFileFromApp(path, newName)) {
                    throw ThrowLastError(path);
                }

                return;
            }

            RecursivelyCopyDirectory(path, newName);
            RecursivelyDeleteDirectory(path);
        }

        private static void RecursivelyCopyDirectory(string path, string newName) {
            if (!FileOrDirectoryExists(newName)) {
                CreateDirectory(newName);
            }

            foreach (var item in GetDirectoryContents(path)) {
                if (item.ItemType == FileOrDirectoryType.Directory) {
                    RecursivelyCopyDirectory(item.Path, Path.Combine(newName, item.Name));
                    continue;
                }

                if (!CopyFileFromApp(item.Path, Path.Combine(newName, item.Name), true)) {
                    throw ThrowLastError(item.Path);
                }
            }
        }

        private static void RecursivelyDeleteDirectory(string path) {
            foreach (var item in GetDirectoryContents(path)) {
                if (item.ItemType == FileOrDirectoryType.Directory) {
                    RecursivelyDeleteDirectory(item.Path);
                    continue;
                }

                if (!DeleteFileFromApp(item.Path)) {
                    throw ThrowLastError(item.Path);
                }
            }

            RemoveDirectory(path);
        }

        public static bool FileOrDirectoryExists(string path) {
            var hFindFile = FindFirstFileExFromApp(
                path, FINDEX_INFO_LEVELS.FindExInfoBasic, out _,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, 0);

            if (hFindFile == Win32ReturnValues.InvalidHandleValue) {
                var error = GetLastError();
                if (!(error == 2 || error == 3)) {
                    throw ThrowLastError(path);
                }

                return false;
            }

            return true;
        }

        private static void CreateDirectories(string path) {
            var (dirname, _) = SplitPath(path);

            if (FileOrDirectoryExists(path)) {
                return;
            }

            if (!FileOrDirectoryExists(dirname)) {
                CreateDirectories(dirname);
            }

            if (!CreateDirectoryFromApp(path)) {
                throw ThrowLastError(path);
            }
        }

        public static void CreateDirectory(string path) {
            if (!CreateDirectoryFromApp(path)) {
                throw ThrowLastError(path);
            }
        }

        public static void RemoveDirectory(string path) {
            if (!RemoveDirectoryFromApp(path)) {
                throw ThrowLastError(path);
            }
        }

        public static IEnumerable<FileOrDirectoryInfo> GetDirectoryContents(string rootPath) {
            var hFindFile = FindFirstFileExFromApp(
                rootPath + @"\*", FINDEX_INFO_LEVELS.FindExInfoBasic, out var findFileData,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, 0);

            if (hFindFile == Win32ReturnValues.InvalidHandleValue) {
                throw ThrowLastError(rootPath);
            }

            do {
                if (findFileData.cFileName == "." || findFileData.cFileName == "..") {
                    continue;
                }


                var path = Path.Combine(rootPath, findFileData.cFileName);

                if (!GetFileAttributesExFromApp(path, GET_FILEEX_INFO_LEVELS.GetFileExInfoStandard, out var fileInformation)) {
                    throw ThrowLastError(path);
                }

                var fileType = ((FileAttributes)fileInformation.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory
                                    ? FileOrDirectoryType.Directory : FileOrDirectoryType.FileOrLink;

                yield return new FileOrDirectoryInfo(path, findFileData.cFileName, fileType);
            } while (FindNextFile(hFindFile, out findFileData));

            _ = FindClose(hFindFile);
        }

        public struct FileOrDirectoryInfo {
            public string Path { get; }
            public string Name { get; }
            public FileOrDirectoryType ItemType { get; }

            public FileOrDirectoryInfo(string path, string fileName, FileOrDirectoryType fileType) {
                this.Path = path;
                this.Name = fileName;
                this.ItemType = fileType;
            }
        }

        public enum FileOrDirectoryType {
            Directory, FileOrLink
        }
    }
}
