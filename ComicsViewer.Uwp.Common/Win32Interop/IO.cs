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
using ComicsViewer.Common;

#nullable enable

namespace ComicsViewer.Uwp.Common.Win32Interop {
    public static class IO {
        #region FindFirstFileEx

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

        //private static class FileAttribute {
        //    public const int Directory = 16;
        //}

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
                MoveFile(path, newName);
                return;
            }

            RecursivelyCopyDirectory(path, newName);
            RecursivelyDeleteDirectory(path);
        }

        /// <summary>
        /// this method will not create intermediate directories.
        /// </summary>
        public static void MoveFile(string path, string newName) {
            if (!MoveFileFromApp(path, newName)) {
                throw General.ThrowLastError(path);
            }
        }

        public static void RemoveFile(string path) {
            if (!DeleteFileFromApp(path)) {
                throw General.ThrowLastError(path);
            }
        }

        public static void CopyFile(string path, string NewName, bool failIfExists = true) {
            if (!CopyFileFromApp(path, NewName, failIfExists)) {
                throw General.ThrowLastError(path);
            }
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

                CopyFile(item.Path, Path.Combine(newName, item.Name));
            }
        }

        private static void RecursivelyDeleteDirectory(string path) {
            foreach (var item in GetDirectoryContents(path)) {
                if (item.ItemType == FileOrDirectoryType.Directory) {
                    RecursivelyDeleteDirectory(item.Path);
                    continue;
                }

                RemoveFile(item.Path);
            }

            RemoveDirectory(path);
        }

        public static bool FileOrDirectoryExists(string path) {
            var hFindFile = FindFirstFileExFromApp(
                path, FINDEX_INFO_LEVELS.FindExInfoBasic, out _,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, 0);

            if (hFindFile == Win32ReturnValues.InvalidHandleValue) {
                var error = General.GetLastError();
                if (!(error == 2 || error == 3)) {
                    throw General.ThrowLastError(path);
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
                throw General.ThrowLastError(path);
            }
        }

        public static void CreateDirectory(string path) {
            if (!CreateDirectoryFromApp(path)) {
                throw General.ThrowLastError(path);
            }
        }

        public static void RemoveDirectory(string path) {
            if (!RemoveDirectoryFromApp(path)) {
                throw General.ThrowLastError(path);
            }
        }

        public static IEnumerable<FileOrDirectoryInfo> GetDirectoryContents(string rootPath) {
            var hFindFile = FindFirstFileExFromApp(
                rootPath + @"\*", FINDEX_INFO_LEVELS.FindExInfoBasic, out var findFileData,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, 0);

            if (hFindFile == Win32ReturnValues.InvalidHandleValue) {
                throw General.ThrowLastError(rootPath);
            }

            do {
                if (findFileData.cFileName == "." || findFileData.cFileName == "..") {
                    continue;
                }


                var path = Path.Combine(rootPath, findFileData.cFileName);

                if (!GetFileAttributesExFromApp(path, GET_FILEEX_INFO_LEVELS.GetFileExInfoStandard, out var fileInformation)) {
                    throw General.ThrowLastError(path);
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
