using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IApplicationUpdateCoordinator: IService
    {
        Task<ApplicationRelease?> CheckAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Выполняет проверку немедленно, игнорируя интервал фоновой проверки.
        /// </summary>
        async Task<ApplicationRelease?> CheckNowAsync(
            CancellationToken cancellationToken = default) =>
            await CheckAsync(cancellationToken);

        Task SkipAsync(
            ApplicationRelease release,
            CancellationToken cancellationToken = default);
    }
}
