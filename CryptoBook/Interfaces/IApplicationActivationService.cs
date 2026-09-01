namespace CryptoBook.Interfaces
{
    public interface IApplicationActivationService: IService, IDisposable
    {
        Task<bool> StartAsync(
            IReadOnlyList<string> commandLineArguments,
            CancellationToken cancellationToken = default);

        void NotifyMainWindowReady(Guid mainWindowId);
    }
}
