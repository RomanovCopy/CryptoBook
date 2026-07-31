using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IAsyncCommand: ICommand
    {
        bool IsRunning { get; }
        Task ExecuteAsync(object? parameter = null);
        void Cancel();
    }
}
