using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Views;

namespace CryptoBook.Services
{
    public sealed class FileConflictResolver: IFileConflictResolver
    {
        private readonly IWindowManager _windowManager;

        public FileConflictResolver(IWindowManager windowManager)
        {
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        }

        public Task<FileConflictDecision> ResolveAsync(
            string sourcePath,
            string destinationPath,
            bool isDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = new Dictionary<string, object?>
            {
                ["sourcePath"] = sourcePath,
                ["destinationPath"] = destinationPath,
                ["isDirectory"] = isDirectory
            };
            Guid windowId = _windowManager.CreateWindow<FileConflictDialog>(context);
            _windowManager.ShowWindowDialog(windowId);
            cancellationToken.ThrowIfCancellationRequested();
            FileConflictDecision decision =
                _windowManager.GetResult<FileConflictDecision?>(windowId)
                ?? new FileConflictDecision(FileConflictAction.Cancel);
            return Task.FromResult(decision);
        }
    }
}
