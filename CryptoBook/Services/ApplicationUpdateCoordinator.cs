using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class ApplicationUpdateCoordinator:
        IApplicationUpdateCoordinator
    {
        private readonly IApplicationUpdateService updateService;
        private readonly IUpdateCheckStateStore stateStore;
        private readonly UpdateCheckOptions options;
        private readonly TimeProvider timeProvider;

        public ApplicationUpdateCoordinator(
            IApplicationUpdateService updateService,
            IUpdateCheckStateStore stateStore,
            UpdateCheckOptions options,
            TimeProvider timeProvider)
        {
            this.updateService = updateService ??
                throw new ArgumentNullException(nameof(updateService));
            this.stateStore = stateStore ??
                throw new ArgumentNullException(nameof(stateStore));
            this.options = options ??
                throw new ArgumentNullException(nameof(options));
            this.timeProvider = timeProvider ??
                throw new ArgumentNullException(nameof(timeProvider));

            if(options.Interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options));
        }

        public async Task<ApplicationRelease?> CheckAsync(
            CancellationToken cancellationToken = default)
            => await CheckInternalAsync(false, cancellationToken);

        public async Task<ApplicationRelease?> CheckNowAsync(
            CancellationToken cancellationToken = default)
            => await CheckInternalAsync(true, cancellationToken);

        private async Task<ApplicationRelease?> CheckInternalAsync(
            bool force,
            CancellationToken cancellationToken)
        {
            UpdateCheckState state = await stateStore.LoadAsync(cancellationToken);
            DateTimeOffset now = timeProvider.GetUtcNow();
            if(!force &&
               state.LastCheckUtc is DateTimeOffset lastCheck &&
               lastCheck <= now &&
               now - lastCheck < options.Interval)
            {
                return null;
            }

            await stateStore.SaveAsync(
                state with { LastCheckUtc = now },
                cancellationToken);

            ApplicationRelease? release = await updateService.CheckAsync(
                cancellationToken);
            if(release is null ||
               (!force &&
               string.Equals(
                   state.SkippedVersion,
                   release.Version.ToString(),
                   StringComparison.Ordinal)))
            {
                return null;
            }

            return release;
        }

        public async Task SkipAsync(
            ApplicationRelease release,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(release);
            UpdateCheckState state = await stateStore.LoadAsync(cancellationToken);
            await stateStore.SaveAsync(
                state with { SkippedVersion = release.Version.ToString() },
                cancellationToken);
        }
    }
}
