using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Tomato.Configuration.Native;

namespace Tomato.Configuration
{
    public class DbStorage : IDisposable
    {
        private Stream fileStorage;

        private const ulong MB = 1024 * 1024;

        public DbStorage(string filePath)
        {
            fileStorage = new FileMappingStream(filePath, 1 * MB);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (fileStorage != null)
                        fileStorage.Dispose();
                }
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
