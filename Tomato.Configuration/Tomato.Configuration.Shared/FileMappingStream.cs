using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Tomato.Configuration.Native;

namespace Tomato.Configuration
{
    /// <summary>
    /// 内存文件映射流
    /// </summary>
    public class FileMappingStream : Stream, IDisposable
    {
        private SafeHandle fileHandle;
        private SafeHandle fileMapping;
        private ViewOfFile viewOfFile;
        private IntPtr currentFileView;

        /// <summary>
        /// 当前大小
        /// </summary>
        private ulong currentSize;

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        private readonly bool canWrite;
        public override bool CanWrite { get { return canWrite; } }

        public override long Length
        {
            get { return (long)currentSize; }
        }

        public override long Position
        {
            get
            {
                if (viewOfFile != null && !viewOfFile.IsInValid)
                    return currentFileView.ToInt64() - viewOfFile.BaseAddress.ToInt64();
                return 0;
            }

            set
            {
                if (value < 0 || value > Length)
                    throw new InvalidOperationException("Position is exceeding the length.");
                currentFileView = new IntPtr(viewOfFile.BaseAddress.ToInt64() + value);
            }
        }

        public FileMappingStream(string filePath, ulong initialSize, bool readOnly = false)
        {
            canWrite = !readOnly;
            fileHandle = Win32Api.CreateFile("123.db", readOnly ? Native.FileAccess.Read :
                Native.FileAccess.Read | Native.FileAccess.Write, Native.ShareMode.Exclusive,
                Native.CreationDisposition.OpenAlways);
            if (initialSize == 0)
                initialSize = (ulong)Win32Api.GetFileSize(fileHandle);
            InitializeFileMapping(initialSize);
        }

        /// <summary>
        /// 增加文件大小
        /// </summary>
        /// <param name="size">增加的大小</param>
        public void IncreaseSize(ulong size)
        {
            var newSize = currentSize + size;
            InitializeFileMapping(newSize);
        }

        private void InitializeFileMapping(ulong size)
        {
            // 保存读写位置
            var oldPosition = Position;
            if (viewOfFile != null)
            {
                viewOfFile.Dispose();
                viewOfFile = null;
            }
            if (fileMapping != null)
            {
                fileMapping.Dispose();
                fileMapping = null;
            }

            fileMapping = Win32Api.CreateFileMapping(fileHandle, IntPtr.Zero,
                CanWrite ? FileMapProtection.PageReadWrite : FileMapProtection.PageReadonly, size);
            viewOfFile = Win32Api.MapViewOfFile(fileMapping, CanWrite ? FileMapAccess.FileMapWrite :
                FileMapAccess.FileMapRead, 0, UIntPtr.Zero);
            currentFileView = viewOfFile.BaseAddress;
            currentSize = size;
            // 恢复读写位置
            Position = oldPosition;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposedValue)
            {
                if (disposing)
                {
                    Flush();
                    if (viewOfFile != null)
                        viewOfFile.Dispose();
                    if (fileMapping != null)
                        fileMapping.Dispose();
                    if (fileHandle != null)
                    {
                        if(CanWrite && !fileHandle.IsInvalid && !fileHandle.IsClosed)
                            Win32Api.FlushFileBuffers(fileHandle);
                        fileHandle.Dispose();
                    }
                }
                disposedValue = true;
            }
        }
        #endregion

        public override void Flush()
        {
            if (CanWrite && viewOfFile != null && !viewOfFile.IsInValid)
            {
                viewOfFile.Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var pointer = EnsureOffsetValid(Position);
            var toRead = Math.Min((int)(Length - Position), count);
            if (toRead == 0) return 0;

            Marshal.Copy(pointer, buffer, offset, toRead);
            Position += toRead;
            return toRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newOffset = Position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newOffset = offset;
                    break;
                case SeekOrigin.Current:
                    newOffset += offset;
                    break;
                case SeekOrigin.End:
                    newOffset = Length + offset;
                    break;
                default:
                    return newOffset;
            }
            Position = newOffset;
            return Position;
        }

        public override void SetLength(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException("value");

            // 增大
            if (value > Length)
                IncreaseSize((ulong)(Length - value));
            // 减小
            else if (value < Length)
                throw new NotSupportedException("Cannot reduce the file size.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
                throw new InvalidOperationException("This file cannot be written.");

            var pointer = EnsureOffsetValid(Position);
            // 长度超出范围则增加
            if (Position + count > Length)
            {
                var increase = Position + count - Length;
                IncreaseSize((ulong)increase);
                pointer = EnsureOffsetValid(Position);
            }
            Marshal.Copy(buffer, offset, pointer, count);
            Position += count;
        }

        IntPtr EnsureOffsetValid(long offset)
        {
            if (disposedValue)
                throw new ObjectDisposedException("this");
            if (viewOfFile == null || viewOfFile.IsInValid)
                throw new InvalidOperationException("Current state is invalid.");

            if (offset < 0 || offset > Length)
                throw new InvalidOperationException("Offset is exceeding file length.");
            return new IntPtr(viewOfFile.BaseAddress.ToInt64() + offset);
        }
    }
}
