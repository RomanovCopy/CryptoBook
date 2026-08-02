using CryptoBook.Interfaces;

namespace CryptoBook.Infrastructure
{
    /// <summary>
    /// Адаптирует асинхронную операцию к ICommand, запрещает повторный запуск
    /// и предоставляет единый токен отмены на время выполнения.
    /// </summary>
    public sealed class AsyncRelayCommand:
        IAsyncCommand,
        IRaiseCanExecuteChanged
    {
        private readonly Func<object?, CancellationToken, Task> execute;
        private readonly Func<object?, bool> canExecute;
        private CancellationTokenSource? cancellation;

        public AsyncRelayCommand(
            Func<object?, CancellationToken, Task> execute,
            Func<object?, bool>? canExecute = null)
        {
            this.execute = execute
                ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute ?? (_ => true);
        }

        public bool IsRunning { get; private set; }

        public event EventHandler? CanExecuteChanged;
        public event EventHandler<Exception>? ExecutionFailed;

        public bool CanExecute(object? parameter) =>
            !IsRunning && canExecute(parameter);

        public async void Execute(object? parameter)
        {
            // ICommand требует void. Исключение переводится в событие, чтобы оно
            // не стало необработанным исключением диспетчера WPF.
            try
            {
                await ExecuteAsync(parameter);
            }
            catch(Exception exception)
            {
                ExecutionFailed?.Invoke(this, exception);
            }
        }

        public async Task ExecuteAsync(object? parameter = null)
        {
            if(!CanExecute(parameter))
                return;

            cancellation = new CancellationTokenSource();
            IsRunning = true;
            RaiseCanExecuteChanged();

            try
            {
                await execute(parameter, cancellation.Token);
            }
            finally
            {
                cancellation.Dispose();
                cancellation = null;
                IsRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        public void Cancel() => cancellation?.Cancel();

        public void RaiseCanExecuteChanged()
        {
            EventHandler? handler = CanExecuteChanged;
            if(handler is null)
                return;

            // CanExecuteChanged может инициироваться рабочим потоком после await,
            // но WPF ожидает уведомление на потоке интерфейса.
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if(dispatcher is null || dispatcher.CheckAccess())
                handler(this, EventArgs.Empty);
            else
                dispatcher.Invoke(() => handler(this, EventArgs.Empty));
        }
    }
}
