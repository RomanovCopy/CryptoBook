using CryptoBook.Interfaces;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System.ComponentModel;
using System.Windows;
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
            var window = (ProgressWindow)host.Window;

            viewModel.Prepare(operationName);
            window.BeginOperation();

            var reporter = new DispatcherProgressReporter(
                _dispatcher,
                viewModel,
                _pathDisplay);
            Task<T>? operationTask = null;
            void OnLoaded(object sender, RoutedEventArgs args)
            {
                window.Loaded -= OnLoaded;
                operationTask = ExecuteOperationAsync();
            }

            // ShowDialog блокирует ввод во всех окнах приложения. Отдельно
            // защищаем владельцев от программного закрытия вместе с операцией.
            var owners = new List<Window>();
            for(Window? owner = window.Owner; owner is not null; owner = owner.Owner)
                owners.Add(owner);
            void BlockOwnerClose(object? sender, CancelEventArgs args)
            {
                args.Cancel = true;
                if(window.IsVisible)
                    _windowManager.ActivateWindow(windowId);
            }
            foreach(Window owner in owners)
                owner.Closing += BlockOwnerClose;
            window.Loaded += OnLoaded;
            try
            {
                // Отложенный показ сохраняет асинхронный контракт RunAsync.
                await _dispatcher.InvokeAsync(() => _windowManager.ShowWindowDialog(windowId));
                return await (operationTask ?? throw new InvalidOperationException(
                    "Окно прогресса не было показано."));
            }
            finally
            {
                window.Loaded -= OnLoaded;
                foreach(Window owner in owners)
                    owner.Closing -= BlockOwnerClose;
                window.CompleteOperation();
                _windowManager.CloseWindow(windowId);
            }

            async Task<T> ExecuteOperationAsync()
            {
                // Даём окну отрисоваться до начала работы.
                await Dispatcher.Yield(DispatcherPriority.Background);
                try
                {
                    return await operation(reporter, viewModel.CancellationToken);
                }
                finally
                {
                    try
                    {
                        await reporter.CompleteAsync();
                    }
                    finally
                    {
                        viewModel.IsOperationRunning = false;
                        window.CompleteOperation();
                        _windowManager.CloseWindow(windowId);
                    }
                }
            }
        }

        internal sealed class DispatcherProgressReporter: IProgressReporter
        {
            private readonly Dispatcher _dispatcher;
            private readonly ProgressViewModel _viewModel;
            private readonly StoragePathDisplayService _pathDisplay;
            private readonly object _sync = new();
            private double? _latestValue;
            private string? _latestInfo;
            private bool _updateQueued;
            private bool _completed;

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
                lock(_sync)
                {
                    if(_completed)
                        return;
                    _latestValue = value;
                    if(!string.IsNullOrWhiteSpace(currentInfo))
                        _latestInfo = currentInfo;
                    if(_updateQueued)
                        return;
                    _updateQueued = true;
                    _dispatcher.BeginInvoke(ApplyPending, DispatcherPriority.Background);
                }
            }

            public async Task CompleteAsync()
            {
                lock(_sync)
                    _completed = true;
                await _dispatcher.InvokeAsync(ApplyPending);
            }

            private void ApplyPending()
            {
                double? value;
                string? info;
                lock(_sync)
                {
                    if(!_updateQueued)
                        return;
                    value = _latestValue;
                    info = _latestInfo;
                    _latestInfo = null;
                    _updateQueued = false;
                }
                _viewModel.Report(value, info is null ? null : _pathDisplay.FormatMessage(info));
            }
        }
    }
}
