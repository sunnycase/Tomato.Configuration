using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Tomato.Configuration.Native
{
    /// <summary>
    /// 文件访问模式
    /// </summary>
    [Flags]
    enum FileAccess : uint
    {
        /// <summary>
        /// 读取模式
        /// </summary>
        Read = 0x80000000u,
        /// <summary>
        /// 写入模式
        /// </summary>
        Write = 0x40000000u
    }

    /// <summary>
    /// 共享模式
    /// </summary>
    [Flags]
    enum ShareMode : uint
    {
        /// <summary>
        /// 独占
        /// </summary>
        Exclusive = 0,
    }

    /// <summary>
    /// 创建设置
    /// </summary>
    enum CreationDisposition : uint
    {
        /// <summary>
        /// 总是创建
        /// </summary>
        CreateAlways = 2,
        /// <summary>
        /// 如果不存在则创建
        /// </summary>
        CreateNew = 1,
        /// <summary>
        /// 打开，如果不存在则创建
        /// </summary>
        OpenAlways = 4
    }

    /// <summary>
    /// 文件映射保护
    /// </summary>
    [Flags]
    enum FileMapProtection : uint
    {
        PageReadonly = 0x02,
        PageReadWrite = 0x04,
        PageWriteCopy = 0x08,
    }

    /// <summary>
    /// 文件映射访问模式
    /// </summary>
    [Flags]
    public enum FileMapAccess : uint
    {
        FileMapCopy = 0x0001,
        FileMapWrite = 0x0002,
        FileMapRead = 0x0004,
        FileMapAllAccess = 0x001f
    }

    class SafeFileHandle : SafeHandle
    {
        public SafeFileHandle(IntPtr handle, bool ownsHandle)
            :base(Win32Api.InvalidHandleValue, ownsHandle)
        {
            this.handle = handle;
        }

        public override bool IsInvalid
        {
            get { return handle == Win32Api.InvalidHandleValue; }
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
                return Win32Api.CloseHandle(handle);
            return true;
        }
    }

    class SafeZeroInvalidHandle : SafeHandle
    {
        public SafeZeroInvalidHandle(IntPtr handle, bool ownsHandle)
            : base(IntPtr.Zero, ownsHandle)
        {
            this.handle = handle;
        }

        public override bool IsInvalid
        {
            get { return handle == IntPtr.Zero; }
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
                return Win32Api.CloseHandle(handle);
            return true;
        }
    }

    class ViewOfFile : IDisposable
    {
        /// <summary>
        /// 基地址
        /// </summary>
        public IntPtr BaseAddress { get; private set; }

        private UIntPtr bytesMapped;

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsInValid
        {
            get { return BaseAddress == IntPtr.Zero; }
        }

        public ViewOfFile(IntPtr baseAddress, UIntPtr bytesMapped)
        {
            BaseAddress = baseAddress;
            this.bytesMapped = bytesMapped;
        }

        public void Flush(UIntPtr bytesToFlush)
        {
            if (!FlushViewOfFile(BaseAddress, bytesToFlush))
                throw new Win32Exception("Cannot flush view of file.", Marshal.GetLastWin32Error());
        }

        public void Flush()
        {
            if (!FlushViewOfFile(BaseAddress, bytesMapped))
                throw new Win32Exception("Cannot flush view of file.", Marshal.GetLastWin32Error());
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if(!IsInValid)
                {
                    UnmapViewOfFile(BaseAddress);
                    BaseAddress = IntPtr.Zero;
                }
                disposedValue = true;
            }
        }
        
        ~ViewOfFile()
        {
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll")]
        private static extern bool FlushViewOfFile(IntPtr lpBaseAddress, UIntPtr dwNumberOfBytesToFlush);
    }

    public class Win32Exception : Exception
    {
        public Win32Exception(string message, int hresult)
            :base(message)
        {
            HResult = hresult;
        }
    }

    static class Win32Api
    {
        public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private extern static IntPtr CreateFile2([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
            [In, MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [In, MarshalAs(UnmanagedType.U4)] ShareMode dwShareMode,
            [In, MarshalAs(UnmanagedType.U4)] CreationDisposition dwCreationDisposition,
            [In, Optional] IntPtr pCreateExParams);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetEndOfFile(IntPtr hFile);

        [DllImport("kernel32.dll")]
        private static extern bool GetFileSizeEx(IntPtr hFile, out long lpFileSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlushFileBuffers(IntPtr hFile);

#if _WINRT
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileMappingFromApp([In] IntPtr hFile,
            [In, Optional] IntPtr lpFileMappingAttributes,
            [In] FileMapProtection flProtect, [In] ulong dwMaximumSize,
            [In, MarshalAs(UnmanagedType.LPWStr), Optional] string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFileFromApp([In] IntPtr hFileMappingObject,
            [In] FileMapAccess dwDesiredAccess,
            [In] ulong dwFileOffset,
            [In] UIntPtr dwNumberOfBytesToMap);
#else
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileMapping([In] IntPtr hFile, 
            [In, Optional] IntPtr lpFileMappingAttributes,
            [In] FileMapProtection flProtect, [In] uint dwMaximumSizeHigh,
            [In] uint dwMaximumSizeLow, 
            [In, MarshalAs(UnmanagedType.LPWStr), Optional] string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile([In] IntPtr hFileMappingObject,
            [In] FileMapAccess dwDesiredAccess,
            [In] uint dwFileOffsetHigh,
            [In] uint dwFileOffsetLow,
            [In] UIntPtr dwNumberOfBytesToMap);
#endif

        public static SafeHandle CreateFile(string lpFileName, FileAccess dwDesiredAccess,
            ShareMode dwShareMode, CreationDisposition dwCreationDisposition, [Optional]IntPtr pCreateExParams)
        {
            var handle = new SafeFileHandle(CreateFile2(lpFileName, dwDesiredAccess, dwShareMode,
                dwCreationDisposition, pCreateExParams), true);

            if (handle.IsInvalid)
                throw new IOException(string.Format("Cannot open or create file: {0}.", lpFileName),
                    Marshal.GetLastWin32Error());
            return handle;
        }

        public static SafeHandle CreateFileMapping(SafeHandle hFile, [Optional] IntPtr lpFileMappingAttributes,
            FileMapProtection flProtect, ulong dwMaximumSize, [Optional] string lpName)
        {

#if _WINRT
            var handle = new SafeZeroInvalidHandle(CreateFileMappingFromApp(hFile.DangerousGetHandle(),
                lpFileMappingAttributes, flProtect, dwMaximumSize, lpName), true);
#else
            var dwMaximumSizeLow = (uint)dwMaximumSize;
            var dwMaximumSizeHigh = (uint)(dwMaximumSize >> 32);

            var handle = new SafeZeroInvalidHandle(CreateFileMapping(hFile.DangerousGetHandle(),
                lpFileMappingAttributes, flProtect, dwMaximumSizeHigh, dwMaximumSizeLow, lpName), true);
#endif

            if (handle.IsInvalid)
                throw new Win32Exception(string.Format("Cannot create file mapping: {0}.", lpName),
                    Marshal.GetLastWin32Error());
            return handle;
        }

        public static ViewOfFile MapViewOfFile(SafeHandle hFileMappingObject, FileMapAccess dwDesiredAccess,
            ulong dwFileOffset, UIntPtr dwNumberOfBytesToMap)
        {
            if (hFileMappingObject.IsInvalid)
                throw new ArgumentException("Invalid file mapping handle.");

#if _WINRT
            var view = new ViewOfFile(MapViewOfFileFromApp(hFileMappingObject.DangerousGetHandle(),
                dwDesiredAccess, dwFileOffset, dwNumberOfBytesToMap), dwNumberOfBytesToMap);
#else
            var dwFileOffsetLow = (uint)dwFileOffset;
            var dwFileOffsetHigh = (uint)(dwFileOffset >> 32);

            var view = new ViewOfFile(MapViewOfFile(hFileMappingObject.DangerousGetHandle(),
                dwDesiredAccess, dwFileOffsetHigh, dwFileOffsetLow, dwNumberOfBytesToMap), 
                dwNumberOfBytesToMap);
#endif
            if (view.IsInValid)
                throw new Win32Exception("Cannot map view of file.", Marshal.GetLastWin32Error());
            return view;
        }

        public static long GetFileSize(SafeHandle file)
        {
            if (file.IsInvalid)
                throw new ArgumentException("Invalid file handle.");
            long size;
            if(!GetFileSizeEx(file.DangerousGetHandle(), out size))
                throw new Win32Exception("Cannot get file size.", Marshal.GetLastWin32Error());
            return size;
        }
        
        public static void FlushFileBuffers(SafeHandle file)
        {
            if (file.IsInvalid)
                throw new ArgumentException("Invalid file handle.");
            if (!FlushFileBuffers(file.DangerousGetHandle()))
                throw new Win32Exception("Cannot flush file buffers.", Marshal.GetLastWin32Error());
        }
    }
}
