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
using Windows.Storage.Streams;
using Windows.Foundation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;

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

        #region File I/O

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile2FromApp(
            string lpFileName,
            Flags.AccessMask dwDesiredAccess,
            Flags.ShareMode dwShareMode,
            Flags.CreationDisposition dwCreationDisposition,
            [Optional]
            IntPtr pCreateExParams
        );

        [DllImport("api-ms-win-core-handle-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("api-ms-win-core-file-l1-2-1.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetFilePointer(
            IntPtr hFile,
            int lDistanceToMove,
            IntPtr lpDistanceToMoveHigh,
            Flags.MoveMethod dwMoveMethod
        );

        [DllImport("api-ms-win-core-file-l1-2-1.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ReadFileEx(
            IntPtr hFile,
            [Out] byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            [In, Out] ref NativeOverlapped lpOverlapped,
            [In] FileIOCompletionRoutine lpCompletionRoutine
        );

        [DllImport("api-ms-win-core-file-l1-2-1.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetFileSizeEx(
            IntPtr hFile,
            out long lpFileSize
        );

        internal delegate void FileIOCompletionRoutine(
             uint dwErrorCode,
             uint dwNumberOfBytesTransfered,
             ref NativeOverlapped lpOverlapped);

        internal static void OnReadComplete(
           uint dwErrorCode,
           uint dwNumberOfBytesTransfered,
           ref NativeOverlapped lpOverlapped
        ) {
            Console.WriteLine($"dwErrorCode: {dwErrorCode}, dwNumberOfBytesTransfered: {dwNumberOfBytesTransfered}");
        }

        [DllImport("api-ms-win-core-synch-l1-2-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern DWORD SleepEx(
            DWORD dwMilliseconds,
            bool  bAlertable
        );


        public class InteropReadStream : IRandomAccessStream, IInputStream {
            private readonly IntPtr handle;

            public InteropReadStream(IntPtr handle, long size) {
                this.handle = handle;
                this._size = (ulong) size;
            }

            public IInputStream GetInputStreamAt(ulong position) {
                return this;
            }

            public void Seek(ulong position) {
                // due to my incompetence, we will simply not allow files larger than 4GB
                if (position > int.MaxValue) {
                    throw new ArgumentException("position must be less than long.MaxValue");
                }

                var lower = (int)(position & 0xFFFFFFFF);
                var pUpper = IntPtr.Zero;


                if (!SetFilePointer(this.handle, lower, pUpper, Flags.MoveMethod.Begin)) {
                    throw General.ThrowLastError();
                }
            }

            public bool CanRead => true;
            public bool CanWrite => false;
            public ulong Position => throw new NotImplementedException();

            private readonly ulong _size;
            public ulong Size {
                get => this._size;
                set => throw new NotImplementedException();
            }

            public class ReadResult: IAsyncOperationWithProgress<IBuffer, uint> {
                private readonly byte[] buffer;
                private NativeOverlapped overlapped;

                public ReadResult(IntPtr handle, uint size) {
                    this.buffer = new byte[size];

                    if (!ReadFileEx(handle, this.buffer, size, ref this.overlapped, this.OnReadComplete)) {
                        throw General.ThrowLastError();
                    }
                }

                public void OnReadComplete(
                    uint dwErrorCode,
                    uint dwNumberOfBytesTransfered,
                    ref NativeOverlapped lpOverlapped
                ) {
                    if (this.Status == AsyncStatus.Canceled) {
                        return;
                    }

                    if (dwErrorCode != 0) {
                        this.Status = AsyncStatus.Error;
                        this.ErrorCode = new Exception($"{dwErrorCode}");
                        this.Completed?.Invoke(this, this.Status);
                        return;
                    }

                    this.Progress?.Invoke(this, dwNumberOfBytesTransfered);

                    this.Status = AsyncStatus.Completed;
                    this.Completed?.Invoke(this, this.Status);
                }

                public IBuffer GetResults() {
                    return this.buffer.AsBuffer();
                }

                public AsyncOperationProgressHandler<IBuffer, uint>? Progress { get; set; }
                public AsyncOperationWithProgressCompletedHandler<IBuffer, uint>? Completed { get; set; }

                public void Cancel() {
                    this.Status = AsyncStatus.Canceled;
                }

                public void Close() {
                    this.Status = AsyncStatus.Canceled;
                }

                public Exception? ErrorCode { get; private set; }
                public uint Id { get; } = 0;
                public AsyncStatus Status { get; private set; } = AsyncStatus.Started;
            }

            public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options) {
                return new ReadResult(this.handle, count);
            }

            public void Dispose() {
                if (!CloseHandle(this.handle)) {
                    throw General.ThrowLastError();
                }
            }

            // These write operations are not supported

            public IOutputStream GetOutputStreamAt(ulong position) {
                throw new NotImplementedException();
            }

            public IRandomAccessStream CloneStream() {
                throw new NotImplementedException();
            }

            public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) {
                throw new NotImplementedException();
            }

            public IAsyncOperation<bool> FlushAsync() {
                throw new NotImplementedException();
            }
        }

        public static IRandomAccessStream OpenFileForRead(string path) {
            var handle = CreateFile2FromApp(
                path,
                Flags.AccessMask.GenericRead,
                Flags.ShareMode.Read,
                Flags.CreationDisposition.OpenExisting
            );

            if (handle == Win32ReturnValues.InvalidHandleValue) {
                throw General.ThrowLastError();
            }

            if (!GetFileSizeEx(handle, out var size)) {
                throw General.ThrowLastError();
            }

            return new InteropReadStream(handle, size);
        }

        #endregion

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
