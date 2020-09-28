using DWORD = System.UInt32;

#nullable enable

namespace ComicsViewer.Uwp.Common.Win32Interop {
    internal static class Flags {
        public enum FormatMessage : DWORD {
            AllocateBuffer = 0x00000100,
            FromSystem = 0x00001000,
            IgnoreInserts = 0x00000200,
        }

        internal enum AccessMask : DWORD {
            /* incomplete */
            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000
        }

        public enum ShareMode : DWORD {
            Read = 1,
            Write = 2,
            Delete = 4
        }

        public enum CreationDisposition : DWORD {
            /// <summary>
            /// Creates a new file. The function fails if a specified file exists.
            /// </summary>
            New = 1,
            /// <summary>
            /// Creates a new file, always.
            /// If a file exists, the function overwrites the file, clears the existing attributes, combines the specified file attributes,
            /// and flags with FILE_ATTRIBUTE_ARCHIVE, but does not set the security descriptor that the SECURITY_ATTRIBUTES structure specifies.
            /// </summary>
            CreateAlways = 2,
            /// <summary>
            /// Opens a file. The function fails if the file does not exist.
            /// </summary>
            OpenExisting = 3,
            /// <summary>
            /// Opens a file, always.
            /// If a file does not exist, the function creates a file as if dwCreationDisposition is CREATE_NEW.
            /// </summary>
            OpenAlways = 4,
            /// <summary>
            /// Opens a file and truncates it so that its size is 0 (zero) bytes. The function fails if the file does not exist.
            /// The calling process must open the file with the GENERIC_WRITE access right.
            /// </summary>
            TruncateExisting = 5
        }

        public enum MoveMethod: DWORD {
            Begin = 0,
            Current = 1,
            End = 2
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
