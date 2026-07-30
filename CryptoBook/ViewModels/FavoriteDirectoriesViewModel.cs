using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class FavoriteDirectoryOpenRequestedEventArgs: EventArgs
    {
        public FavoriteDirectoryOpenRequestedEventArgs(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }

    public sealed class FavoriteDirectoriesViewModel:
        ViewModelBase,
        IFavoriteDirectoriesViewModel,
        IDisposable
    {
        private readonly IFavoriteDirectoryService _service;
        private readonly ITextInputService _textInputService;
        private readonly IMessageService _messageService;
        private readonly ObservableCollection<FavoriteDirectoryItemViewModel> _items = [];
        private bool _initialized;

        public FavoriteDirectoriesViewModel(
            IFavoriteDirectoryService service,
            ITextInputService textInputService,
            IMessageService messageService)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _textInputService = textInputService
                ?? throw new ArgumentNullException(nameof(textInputService));
            _messageService = messageService
                ?? throw new ArgumentNullException(nameof(messageService));
            Items = new ReadOnlyObservableCollection<FavoriteDirectoryItemViewModel>(_items);
        }

        public event EventHandler<FavoriteDirectoryOpenRequestedEventArgs>? OpenRequested;
        public ReadOnlyObservableCollection<FavoriteDirectoryItemViewModel> Items { get; }

        public ICommand AddCurrentDirectoryCommand => _addCurrentDirectoryCommand
            ??= new RelayCommand(AddCurrentDirectory, CanAddCurrentDirectory);
        private RelayCommand? _addCurrentDirectoryCommand;

        public ICommand OpenCommand => _openCommand
            ??= new RelayCommand(Open, item => item is FavoriteDirectoryItemViewModel { IsAvailable: true });
        private RelayCommand? _openCommand;

        public ICommand RenameCommand => _renameCommand
            ??= new RelayCommand(Rename, item => item is FavoriteDirectoryItemViewModel);
        private RelayCommand? _renameCommand;

        public ICommand RemoveCommand => _removeCommand
            ??= new RelayCommand(Remove, item => item is FavoriteDirectoryItemViewModel);
        private RelayCommand? _removeCommand;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if(_initialized)
            {
                await RefreshAvailabilityAsync(cancellationToken);
                return;
            }

            await _service.InitializeAsync(cancellationToken);
            _initialized = true;
            _service.Changed += Service_Changed;
            await RebuildAsync(cancellationToken);
        }

        private bool CanAddCurrentDirectory(object? parameter) =>
            parameter is string path && !string.IsNullOrWhiteSpace(path);

        private async void AddCurrentDirectory(object? parameter)
        {
            if(parameter is not string path || string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                await _service.AddAsync(path);
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    "Ошибка избранного",
                    $"Не удалось добавить директорию:\r\n{ex.Message}");
            }
        }

        private void Open(object? parameter)
        {
            if(parameter is FavoriteDirectoryItemViewModel { IsAvailable: true } item)
                OpenRequested?.Invoke(this, new FavoriteDirectoryOpenRequestedEventArgs(item.Path));
        }

        private async void Rename(object? parameter)
        {
            if(parameter is not FavoriteDirectoryItemViewModel item)
                return;

            string? name = _textInputService.Request(
                "Переименование закладки",
                "Введите отображаемое имя:",
                item.DisplayName,
                "Сохранить");
            if(name is null)
                return;

            try
            {
                await _service.RenameAsync(item.Id, name);
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    "Ошибка избранного",
                    $"Не удалось переименовать закладку:\r\n{ex.Message}");
            }
        }

        private async void Remove(object? parameter)
        {
            if(parameter is not FavoriteDirectoryItemViewModel item)
                return;

            try
            {
                await _service.RemoveAsync(item.Id);
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    "Ошибка избранного",
                    $"Не удалось удалить закладку:\r\n{ex.Message}");
            }
        }

        private async Task RebuildAsync(CancellationToken cancellationToken)
        {
            _items.Clear();
            foreach(var favorite in _service.Items)
            {
                var item = new FavoriteDirectoryItemViewModel(
                    favorite,
                    _service.GetDisplayPath(favorite.Path));
                _items.Add(item);
            }

            await RefreshAvailabilityAsync(cancellationToken);
        }

        private async Task RefreshAvailabilityAsync(CancellationToken cancellationToken)
        {
            foreach(var item in _items)
            {
                try
                {
                    item.IsAvailable = await _service.IsAvailableAsync(
                        item.Path,
                        cancellationToken);
                }
                catch(OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    item.IsAvailable = false;
                }
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private async void Service_Changed(object? sender, EventArgs e)
        {
            try
            {
                await RebuildAsync(CancellationToken.None);
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    "Ошибка избранного",
                    $"Не удалось обновить список избранного:\r\n{ex.Message}");
            }
        }

        public void Dispose()
        {
            _service.Changed -= Service_Changed;
        }
    }
}
