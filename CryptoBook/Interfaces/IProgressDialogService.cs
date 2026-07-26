namespace CryptoBook.Interfaces
{
    public interface IProgressDialogService
    {
        Task<T> RunAsync<T>(
            string operationName,
            Func<IProgressReporter, CancellationToken, Task<T>> operation);
    }
}
