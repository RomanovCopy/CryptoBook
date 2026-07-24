using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class FileSecurityService:IFileSecurityService
    {

        private readonly ISystemItemCreateService _createService;
        private readonly ISecureFileProcessor _secureFileProcessor;

        public FileSecurityService(ISystemItemCreateService createService, ISecureFileProcessor secureFileProcessor) 
        {
            _createService = createService??throw new ArgumentNullException(nameof(createService));
            _secureFileProcessor = secureFileProcessor??throw new ArgumentNullException(nameof(secureFileProcessor));
        }


        public Task<FileOperationResult> EncryptAsync(ISystemItem source, string destinationPath, EncryptionTargetMode mode, IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            switch(source.GetType())
            {
                case IDirectoryItem directoryItem:
                {
                    break;
                }
                case IFileItem fileItem:
                {
                    break;
                }
            }
        }

        public Task<FileOperationResult> DecryptAsync(ISystemItem source, string destinationPath, EncryptionTargetMode mode, IProgressReporter? progress = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
