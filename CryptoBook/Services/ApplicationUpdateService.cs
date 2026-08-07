using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class ApplicationUpdateService: IApplicationUpdateService
    {
        private readonly IReleaseSource releaseSource;
        private readonly IApplicationVersionProvider versionProvider;

        public ApplicationUpdateService(
            IReleaseSource releaseSource,
            IApplicationVersionProvider versionProvider)
        {
            this.releaseSource = releaseSource ??
                throw new ArgumentNullException(nameof(releaseSource));
            this.versionProvider = versionProvider ??
                throw new ArgumentNullException(nameof(versionProvider));
        }

        public async Task<ApplicationRelease?> CheckAsync(
            CancellationToken cancellationToken = default)
        {
            ApplicationRelease? latest = await releaseSource.GetLatestAsync(
                cancellationToken);
            if(latest is null)
                return null;

            SemanticVersion current = versionProvider.GetCurrentVersion();
            return latest.Version.CompareTo(current) > 0
                ? latest
                : null;
        }
    }
}
