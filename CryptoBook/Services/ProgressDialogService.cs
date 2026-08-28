using CryptoBook.Interfaces;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System.Windows.Threading;

namespace CryptoBook.Services
{
    public sealed class ProgressDialogService: IProgressDialogService
    {
        private readonly IWindowManager _windowManager;
        private readonly Dispatcher _dispatcher;
        private readonly StoragePathDisplayService _pathDisplay;

        public ProgressDialogService(
            IWindowManager windowManager,
            Dispatcher dispatcher,
            StoragePathDisplayService pathDisplay)
        {
            _windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
            _dispatcher = dispatcher ??
                throw new ArgumentNullException(nameof(dispatcher));
            _pathDisplay = pathDisplay ??
                throw new ArgumentNullException(nameof(pathDisplay));
        }

        public async Task<T> RunAsync<T>(
            string operationName,
            Func<IProgressReporter, CancellationToken, Task<T>> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            Guid windowId = _windowManager.CreateWindow<ProgressWindow>();
            var host = _windowManager.FindHostWindow(windowId)
                ?? throw new InvalidOperationException("Не удалось создать окно прогресса.");
            var viewModel = host.Window.DataContext as ProgressViewModel
                ?? throw new InvalidOperationException("Окно прогресса использует неподдерживаемую модель.");

            viewModel.Prepare(operationName);
            _windowManager.ShowWindow(windowId);
            await Dispatcher.Yield(DispatcherPriority.Background);

            var reporter = new DispatcherProgressReporter(
                _dispatcher,
                viewModel,
                _pathDisplay);
            try
            {
                return await operation(reporter, viewModel.CancellationToken);
            }
            finally
            {
                // Окно остаётся открытым, пока операция действительно не завершится.
                await _dispatcher.InvokeAsync(() => _windowManager.CloseWindow(windowId));
            }
        }

        private sealed class DispatcherProgressReporter: IProgressReporter
        {
            private readonly Dispatcher _dispatcher;
            private readonly ProgressViewModel _viewModel;
            private readonly StoragePathDisplayService _pathDisplay;

            public DispatcherProgressReporter(
                Dispatcher dispatcher,
                ProgressViewModel viewModel,
                StoragePathDisplayService pathDisplay)
            {
                _dispatcher = dispatcher;
                _viewModel = viewModel;
                _pathDisplay = pathDisplay;
            }

            public void Report(double? value, string? currentInfo = null)
            {
                string? displayInfo = currentInfo is null
                    ? null
                    : _pathDisplay.FormatMessage(currentInfo);
                _dispatcher.BeginInvoke(() => _viewModel.Report(value, displayInfo));
            }
        }
    }
}
