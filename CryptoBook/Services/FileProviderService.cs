using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class FileProviderService:IFileProviderService
    {
        public string Scheme => throw new NotImplementedException();

        public Task<IDirectoryContent> GetDirectoryContentAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IFileOperationResult> CopyAsync(string sourcePath, string destinationPath, IProgressReporter? progress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IFileOperationResult> MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IFileOperationResult> DeleteAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IFileOperationResult> RenameAsync(string path, string newName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CanReadAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CanWriteAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IFileOperationResult> CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
