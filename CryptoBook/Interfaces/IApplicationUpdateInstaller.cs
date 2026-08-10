using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    /// <summary>Downloads and starts the Inno Setup package for a release.</summary>
    public interface IApplicationUpdateInstaller
    {
        Task InstallAsync(
            ApplicationRelease release,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default);
    }
}
