using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace CryptoBook.ViewModels
{
    public class FileExplorerViewModel: ViewModelBase, IFileExplorerViewModel
    {
        public const int FilterDebounceMilliseconds = 200;

        private readonly IFileExplorerModel _fileExplorerModel;
        private readonly IMessageService _messageService;
        private readonly IWindowManager _windowManager;
        private readonly IFilePropertiesService _filePropertiesService;
        private readonly FileExplorerMode _mode;
        private readonly string? _initialDirectory;
        private ISystemItem? _previewSelection;
        private readonly ObservableCollection<ISystemItem> _emptyChildren = [];
        private readonly DispatcherTimer _filterDebounceTimer;

        public double WindowWidth { get =>_fileExplorerModel.WindowWidth; set => _fileExplorerModel.WindowWidth=value; }
        public double WindowHeight { get => _fileExplorerModel.WindowHeight; set => _fileExplorerModel.WindowHeight=value; }
        public double WindowTop { get => _fileExplorerModel.WindowTop; set => _fileExplorerModel.WindowTop=value; }
        public double WindowLeft { get => _fileExplorerModel.WindowLeft; set => _fileExplorerModel.WindowLeft=value; }
        public WindowState WindowState { get => _fileExplorerModel.WindowState; set => _fileExplorerModel.WindowState=value; }


        public double LeftColumnPercent { get => _fileExplorerModel.LeftColumnPercent; set => _fileExplorerModel.LeftColumnPercent=value; }
        public double RightColumnPercent { get => _fileExplorerModel.RightColumnPercent; set => _fileExplorerModel.RightColumnPercent = value; }

        public bool IsHiddenFilesVisible { get => _fileExplorerModel.IsHiddenFilesVisible; set => _fileExplorerModel.IsHiddenFilesVisible=value; }
        public ISystemItem? SelectedItem { get => _fileExplorerModel.SelectedItem; set => _fileExplorerModel.SelectedItem=value; }
        public ISystemItem? SelectedListItem { get => _fileExplorerModel.SelectedListItem; set => _fileExplorerModel.SelectedListItem=value; }
        public IReadOnlyList<ISystemItem> SelectedItemsSnapshot { get => _fileExplorerModel.SelectedItemsSnapshot; set => _fileExplorerModel.SelectedItemsSnapshot=value; }
        public ReadOnlyObservableCollection<IDriveItem> GetDrives => _fileExplorerModel.GetDrives;
        public string CurrentPath => _fileExplorerModel.CurrentPath;
        public string AddressText { get => _fileExplorerModel.AddressText; set => _fileExplorerModel.AddressText=value; }
        public bool IsCurrentDirectoryUnavailable =>
            _fileExplorerModel.IsCurrentDirectoryUnavailable;
        public FileExplorerNavigationErrorKind? LastNavigationError =>
            _fileExplorerModel.LastNavigationError;
        public string NavigationErrorMessage =>
            _fileExplorerModel.NavigationErrorMessage;
        public string ExplorerTitle => LocalizationManager.GetString(_mode switch
        {
            FileExplorerMode.SelectFile => "File.PickerTitle",
            FileExplorerMode.SelectFolder => "File.NewFolderPickerDescription",
            _ => "Explorer.WindowTitle"
        });
        public string PickerActionText => LocalizationManager.GetString(
            _mode == FileExplorerMode.SelectFolder
                ? "Explorer.Picker.SelectFolder"
                : "Explorer.Picker.SelectFile");
        public bool IsPickerMode => _mode != FileExplorerMode.Manage;
        public string PickerSelectionPath => ResolvePickerSelection() ?? string.Empty;
        public string? Result { get; private set; }
        public bool HasResult => Result is not null;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if(!SetProperty(ref _filterText, value))
                    return;

                _filterDebounceTimer.Stop();
                _filterDebounceTimer.Start();
            }
        }
        private string _filterText = string.Empty;
        public ICollectionView ChildrenView
        {
            get => _childrenView;
            private set => SetProperty(ref _childrenView, value);
        }
        private ICollectionView _childrenView = null!;
        public Guid WindowId => _fileExplorerModel.WindowId;
        public IFavoriteDirectoriesViewModel Favorites { get; }
        public IFilePreviewViewModel Preview { get; }



        public FileExplorerViewModel(
            IFileExplorerModel fileExplorerModel,
            IFavoriteDirectoriesViewModel favorites,
            IFilePreviewViewModel preview,
            IMessageService messageService,
            IFilePropertiesService filePropertiesService,
            IWindowManager windowManager,
            IWindowContext windowContext)
        {
            _fileExplorerModel = fileExplorerModel ?? throw new ArgumentNullException(nameof(fileExplorerModel));
            Favorites = favorites ?? throw new ArgumentNullException(nameof(favorites));
            Preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _filePropertiesService = filePropertiesService ??
                throw new ArgumentNullException(nameof(filePropertiesService));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            ArgumentNullException.ThrowIfNull(windowContext);
            _mode = windowContext.TryGet<FileExplorerMode>(
                FileExplorerService.ModeContextKey,
                out FileExplorerMode mode)
                ? mode
                : FileExplorerMode.Manage;
            _initialDirectory = windowContext.TryGet<string>(
                FileExplorerService.InitialDirectoryContextKey,
                out string initialDirectory)
                ? initialDirectory
                : null;
            _filterDebounceTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(FilterDebounceMilliseconds),
                DispatcherPriority.Background,
                FilterDebounceTimer_Tick,
                Dispatcher.CurrentDispatcher);
            _fileExplorerModel.PropertyChanged += FileExplorerModel_PropertyChanged;
            Favorites.OpenRequested += Favorites_OpenRequested;
            LocalizationManager.CultureChanged += OnCultureChanged;
            RebuildChildrenView();
        }

        private void FileExplorerModel_PropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
            if(e.PropertyName == nameof(SelectedItem))
                RebuildChildrenView();
            if(e.PropertyName is nameof(SelectedItem) or
                nameof(SelectedListItem) or
                nameof(SelectedItemsSnapshot) or
                nameof(CurrentPath) or
                nameof(IsCurrentDirectoryUnavailable))
            {
                OnPropertyChanged(nameof(PickerSelectionPath));
                _confirmSelectionCommand?.RaiseCanExecuteChanged();
            }
        }

        private void RebuildChildrenView()
        {
            if(_childrenView is not null)
                _childrenView.Filter = null;

            object source = SelectedItem is IContainerSystemItem container
                ? container.Children
                : _emptyChildren;
            ICollectionView view = CollectionViewSource.GetDefaultView(source);
            view.Filter = item =>
                item is ISystemItem systemItem &&
                FileExplorerItemFilter.Matches(systemItem, FilterText);
            ChildrenView = view;
        }

        private void FilterDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _filterDebounceTimer.Stop();
            ChildrenView.Refresh();
        }

        private async void Favorites_OpenRequested(
            object? sender,
            FavoriteDirectoryOpenRequestedEventArgs e)
        {
            try
            {
                await _fileExplorerModel.NavigateAsync(
                    e.Path,
                    FileExplorerNavigationMode.Standard);
            }
            catch(OperationCanceledException)
            {
            }
        }


        public ICommand BackCommand => _backCommand ??= new RelayCommand(_fileExplorerModel.Execute_BackCommand, _fileExplorerModel.CanExecute_BackCommand);
        RelayCommand _backCommand;

        public ICommand ForwardCommand => _forwardCommand ??= new RelayCommand(_fileExplorerModel.Execute_ForwardCommand, _fileExplorerModel.CanExecute_ForwardCommand);
        RelayCommand _forwardCommand;

        public ICommand UpCommand => _upCommand ??= new RelayCommand(_fileExplorerModel.Execute_UpCommand, _fileExplorerModel.CanExecute_UpCommand);
        RelayCommand _upCommand;

        public ICommand ApplyAddressCommand => _applyAddressCommand ??= new RelayCommand(_fileExplorerModel.Execute_ApplyAddressCommand, _fileExplorerModel.CanExecute_ApplyAddressCommand);
        RelayCommand _applyAddressCommand;

        public ICommand CancelAddressCommand => _cancelAddressCommand ??= new RelayCommand(_fileExplorerModel.Execute_CancelAddressCommand);
        RelayCommand _cancelAddressCommand;

        public ICommand RetryNavigationCommand => _retryNavigationCommand ??=
            new RelayCommand(
                _fileExplorerModel.Execute_RetryNavigationCommand,
                _fileExplorerModel.CanExecute_RetryNavigationCommand);
        RelayCommand _retryNavigationCommand;

        public ICommand CurrentDocumentCommand => _currentDocumentCommand ??= new RelayCommand(_fileExplorerModel.Execute_CurrentDocumentCommand, _fileExplorerModel.CanExecute_CurrentDocumentCommand);
        RelayCommand _currentDocumentCommand;

        public ICommand OpenCommand => _openCommand ??= new RelayCommand(
            ExecuteOpen,
            CanExecuteOpen);
        RelayCommand _openCommand;

        public ICommand OpenWithCommand => _openWithCommand ??= new RelayCommand(_fileExplorerModel.Execute_OpenWithCommand, _fileExplorerModel.CanExecute_OpenWithCommand);
        RelayCommand _openWithCommand;

        public ICommand RevealInExplorerCommand => _revealInExplorerCommand ??= new RelayCommand(_fileExplorerModel.Execute_RevealInExplorerCommand, _fileExplorerModel.CanExecute_RevealInExplorerCommand);
        RelayCommand _revealInExplorerCommand;

        public ICommand PropertiesCommand => _propertiesCommand ??=
            new RelayCommand(ExecuteProperties, CanExecuteProperties);
        RelayCommand _propertiesCommand;

        private bool CanExecuteProperties(object? parameter) =>
            !IsCurrentDirectoryUnavailable &&
            FileExplorerSelectionPolicy.IsSingle(parameter);

        private async void ExecuteProperties(object? parameter)
        {
            IReadOnlyList<ISystemItem> selection =
                FileExplorerSelectionPolicy.CreateSnapshot(parameter);
            if(selection.Count != 1)
                return;

            LaunchResult result = _filePropertiesService.Show(
                selection[0].FullPath);
            if(!result.Success)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.PropertiesError"),
                    result.Error);
            }
        }

        public ICommand CopyPathCommand => _copyPathCommand ??= new RelayCommand(_fileExplorerModel.Execute_CopyPathCommand, _fileExplorerModel.CanExecute_CopyPathCommand);
        RelayCommand _copyPathCommand;

        public ICommand CutCommand => _cutCommand ??= new RelayCommand(_fileExplorerModel.Execute_CutCommand, _fileExplorerModel.CanExecute_CutCommand);
        RelayCommand _cutCommand;

        public ICommand CopyCommand => _copyCommand ??= new RelayCommand(_fileExplorerModel.Execute_CopyCommand, _fileExplorerModel.CanExecute_CopyCommand);
        RelayCommand _copyCommand;

        public ICommand PasteCommand => _pasteCommand ??= new RelayCommand(_fileExplorerModel.Execute_PasteCommand, _fileExplorerModel.CanExecute_PasteCommand);
        RelayCommand _pasteCommand;

        public ICommand DeleteCommand => _deleteCommand ??= new RelayCommand(_fileExplorerModel.Execute_DeleteCommand, _fileExplorerModel.CanExecute_DeleteCommand);
        RelayCommand _deleteCommand;

        public ICommand SortedCommand => _sortedCommand ??= new RelayCommand(_fileExplorerModel.Execute_SortedCommand, _fileExplorerModel.CanExecute_SortedCommand);
        RelayCommand _sortedCommand;

        public ICommand CreateFileCommand => _createFileCommand ??= new RelayCommand(_fileExplorerModel.Execute_CreateFileCommand, _fileExplorerModel.CanExecute_CreateFileCommand);
        RelayCommand _createFileCommand;

        public ICommand CreateDirectoryCommand => _createDirectoryCommand ??= new RelayCommand(_fileExplorerModel.Execute_CreateDirectoryCommand, _fileExplorerModel.CanExecute_CreateDirectoryCommand);
        RelayCommand _createDirectoryCommand;

        public ICommand RenameClickCommand => _renameClickCommand ??= new RelayCommand(_fileExplorerModel.Execute_RenameClickCommand, _fileExplorerModel.CanExecute_RenameClickCommand);
        RelayCommand _renameClickCommand;


        public ICommand RenameCommand => _renameFileCommand ??= new RelayCommand(_fileExplorerModel.Execute_RenameCommand, _fileExplorerModel.CanExecute_RenameCommand);
        RelayCommand _renameFileCommand;


        public ICommand MoveCommand => _moveCommand ??= new RelayCommand(_fileExplorerModel.Execute_MoveCommand, _fileExplorerModel.CanExecute_MoveCommand);
        RelayCommand _moveCommand;

        public ICommand DropCommand => _dropCommand ??= new RelayCommand(
            _fileExplorerModel.Execute_DropCommand,
            _fileExplorerModel.CanExecute_DropCommand);
        RelayCommand _dropCommand;

        public ICommand RefreshCommand => _refreshCommand ??= new RelayCommand(_fileExplorerModel.Execute_RefreshCommand, _fileExplorerModel.CanExecure_RefreshCommand);
        RelayCommand _refreshCommand;

        public ICommand CancelRenameCommand => _cancelRenameCommand ??= new RelayCommand(_fileExplorerModel.Execute_CancelRenameCommand, _fileExplorerModel.CanExecute_CancelRenameCommand);
        RelayCommand _cancelRenameCommand;

        public ICommand TreeViewItemSelectedCommand => _treeViewItemSelectedCommand ??= new RelayCommand(_fileExplorerModel.Execute_TreeViewItemSelectedCommand, _fileExplorerModel.CanExecute_TreeViewItemSelectedCommand);
        RelayCommand _treeViewItemSelectedCommand;

        public ICommand ListViewItemDoubleClickCommand => _listViewItemDoubleClickCommand??= new RelayCommand(
            ExecuteListViewItemDoubleClick,
            CanExecuteListViewItemDoubleClick);
        RelayCommand _listViewItemDoubleClickCommand;

        public ICommand ListViewSelectionChangedCommand => _listViewSelectionChangedCommand
            ??= new RelayCommand(
                ExecuteListViewSelectionChanged,
                _ => true);
        RelayCommand _listViewSelectionChangedCommand;

        private async void ExecuteListViewSelectionChanged(object? parameter)
        {
            _fileExplorerModel.Execute_ListViewSelectionChangedCommand(parameter);
            _previewSelection = parameter as ISystemItem;
            await Preview.SelectAsync(_previewSelection);
        }


        public ICommand WindowSizeChangedCommand => _windowSizeChangedCommand ??= new RelayCommand(_fileExplorerModel.Execute_WindowSizeChanged, _fileExplorerModel.CanExecute_WindowSizeChanged);
        RelayCommand _windowSizeChangedCommand;


        public ICommand Loaded => _loadedCommand ??= new RelayCommand(ExecuteLoaded, _fileExplorerModel.CanExecute_Loaded);
        RelayCommand _loadedCommand;

        private async void ExecuteLoaded(object? parameter)
        {
            _fileExplorerModel.Execute_Loaded(parameter);
            try
            {
                if(!await NavigateToInitialDirectoryAsync())
                    await _fileExplorerModel.RestoreLastDirectoryAsync();
                await Favorites.InitializeAsync();
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Favorites.ErrorTitle"),
                    LocalizationManager.Format(
                        "Favorites.RefreshFailed",
                        Environment.NewLine,
                        ex.Message));
            }
        }

        public ICommand Close => _closeCommand ??= new RelayCommand(_fileExplorerModel.Execute_Close, _fileExplorerModel.CanExecute_Close);
        RelayCommand _closeCommand;

        public ICommand Closing => _closingCommand ??= new RelayCommand(_fileExplorerModel.Execute_Closing, _fileExplorerModel.CanExecute_Closing);
        RelayCommand _closingCommand;

        public ICommand Closed => _closedCommand ??= new RelayCommand(ExecuteClosed, _fileExplorerModel.CanExecute_Closed);
        RelayCommand _closedCommand;

        private void ExecuteClosed(object? parameter)
        {
            _filterDebounceTimer.Stop();
            _fileExplorerModel.PropertyChanged -= FileExplorerModel_PropertyChanged;
            Favorites.OpenRequested -= Favorites_OpenRequested;
            LocalizationManager.CultureChanged -= OnCultureChanged;
            if(_childrenView is not null)
                _childrenView.Filter = null;
            _fileExplorerModel.Execute_Closed(parameter);
        }

        public ICommand ConfirmSelectionCommand => _confirmSelectionCommand ??=
            new RelayCommand(ConfirmSelection, _ => CanConfirmSelection());
        RelayCommand? _confirmSelectionCommand;

        public ICommand CancelSelectionCommand => _cancelSelectionCommand ??=
            new RelayCommand(_ => _windowManager.CloseWindow(WindowId));
        RelayCommand? _cancelSelectionCommand;

        private bool CanExecuteOpen(object? parameter)
        {
            if(!IsPickerMode)
                return _fileExplorerModel.CanExecute_OpenCommand(parameter);

            ISystemItem? item = GetSingleSelection(parameter);
            return item is IContainerSystemItem ||
                (_mode == FileExplorerMode.SelectFile && item is IFileItem);
        }

        private void ExecuteOpen(object? parameter)
        {
            ISystemItem? item = GetSingleSelection(parameter);
            if(IsPickerMode && item is IFileItem)
            {
                ConfirmSelection(null);
                return;
            }

            _fileExplorerModel.Execute_OpenCommand(parameter);
        }

        private void ExecuteListViewItemDoubleClick(object? parameter)
        {
            ISystemItem? item = GetSingleSelection(parameter);
            if(_mode == FileExplorerMode.SelectFile && item is IFileItem)
            {
                ConfirmSelection(null);
                return;
            }

            _fileExplorerModel.Execute_ListViewItemDoubleClickCommand(parameter);
        }

        private bool CanExecuteListViewItemDoubleClick(object? parameter)
        {
            if(_mode != FileExplorerMode.SelectFolder)
            {
                return _fileExplorerModel
                    .CanExecute_ListViewItemDoubleClickCommand(parameter);
            }

            return GetSingleSelection(parameter) is IContainerSystemItem;
        }

        private bool CanConfirmSelection() =>
            IsPickerMode &&
            !IsCurrentDirectoryUnavailable &&
            !string.IsNullOrWhiteSpace(ResolvePickerSelection());

        private static ISystemItem? GetSingleSelection(object? parameter)
        {
            IReadOnlyList<ISystemItem> selection =
                FileExplorerSelectionPolicy.NormalizeForOperation(parameter);
            return selection.Count == 1 ? selection[0] : null;
        }

        private void ConfirmSelection(object? parameter)
        {
            string? selection = ResolvePickerSelection();
            if(string.IsNullOrWhiteSpace(selection))
                return;

            Result = selection;
            _windowManager.CloseWindow(WindowId);
        }

        private string? ResolvePickerSelection()
        {
            if(_mode == FileExplorerMode.SelectFile)
                return SelectedListItem is IFileItem file ? file.FullPath : null;
            if(_mode != FileExplorerMode.SelectFolder)
                return null;

            return SelectedListItem is IContainerSystemItem directory
                ? directory.FullPath
                : string.IsNullOrWhiteSpace(CurrentPath) ? null : CurrentPath;
        }

        private async Task<bool> NavigateToInitialDirectoryAsync()
        {
            if(string.IsNullOrWhiteSpace(_initialDirectory))
                return false;

            string path = _initialDirectory;
            const string localPrefix = "local://";
            if(path.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
                path = path[localPrefix.Length..];
            if(File.Exists(path))
                path = Path.GetDirectoryName(path) ?? path;

            return await _fileExplorerModel.NavigateAsync(
                path,
                FileExplorerNavigationMode.Restore);
        }

        private void OnCultureChanged(object? sender, EventArgs args)
        {
            OnPropertyChanged(nameof(ExplorerTitle));
            OnPropertyChanged(nameof(PickerActionText));
        }

        public ICommand EncryptingKeyCommand => _encryptingKeyCommand
            ??= new RelayCommand(
                ExecuteEncryptingKeyCommand,
                _fileExplorerModel.CanExecute_EncryptingKeyCommand);
        RelayCommand _encryptingKeyCommand;

        private async void ExecuteEncryptingKeyCommand(object? parameter)
        {
            _fileExplorerModel.Execute_EncryptingKeyCommand(parameter);
            if(_previewSelection is not null)
                await Preview.SelectAsync(_previewSelection);
        }

        public ICommand DecryptCommand => _decryptCommand ??= new RelayCommand(
            ExecuteDecryptCommand,
            _fileExplorerModel.CanExecute_DecryptCommand);
        RelayCommand _decryptCommand;

        private void ExecuteDecryptCommand(object? parameter)
        {
            ClearPreviewBeforeFileMutation();
            _fileExplorerModel.Execute_DecryptCommand(parameter);
        }

        public ICommand EncryptCommand => _encryptCommand ??= new RelayCommand(
            ExecuteEncryptCommand,
            _fileExplorerModel.CanExecute_EncryptCommand);
        RelayCommand _encryptCommand;

        private void ExecuteEncryptCommand(object? parameter)
        {
            ClearPreviewBeforeFileMutation();
            _fileExplorerModel.Execute_EncryptCommand(parameter);
        }

        private void ClearPreviewBeforeFileMutation()
        {
            // Не оставляем устаревший preview для файла, который сейчас будет
            // атомарно заменён или удалён при расшифровке.
            _previewSelection = null;
            Preview.Clear();
        }
    }
}
