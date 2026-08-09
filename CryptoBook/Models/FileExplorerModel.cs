using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Properties;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Models
{
    public class FileExplorerModel: ViewModelBase, IFileExplorerModel
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly IFileManagerService _fileManagerService;
        private readonly IWindowManager _windowManager;
        private readonly IDriveManagerService _driveManagerService;
        private readonly IFileClipboardService _fileClipboardService;
        private readonly IFolderPickerService _folderPickerService;
        private readonly IMessageService _messageService;
        private readonly IKeyProvider _keyProvider;
        private readonly IFileSecurityService _fileSecurityService;
        private readonly ISystemItemCreateService _systemItemCreateService;
        private readonly IProgressDialogService _progressDialogService;
        private readonly IFileOperationCoordinator _fileOperationCoordinator;
        private readonly IWorkspaceFileOpenService _fileOpenService;
        private readonly IFileLauncherService _fileLauncherService;
        private readonly IDocumentSession _documentSession;
        private readonly IPinnedDocumentService _pinnedDocumentService;
        private readonly IRecentDocumentService? _recentDocumentService;
        private readonly FileExplorerNavigationHistory _navigationHistory = new();
        private INotifyCollectionChanged? _currentContainerAvailabilitySource;

        private CancellationTokenSource _cancellationTokenSource = new();

        public double WindowWidth { get => _windowWidth; set => SetProperty(ref _windowWidth, value); }
        private double _windowWidth;
        public double WindowHeight { get => _windowHeight; set => SetProperty(ref _windowHeight, value); }
        private double _windowHeight;
        public double WindowTop { get => _windowTop; set => SetProperty(ref _windowTop, value); }
        private double _windowTop;
        public double WindowLeft { get => _windowLeft; set => SetProperty(ref _windowLeft, value); }
        private double _windowLeft;
        public WindowState WindowState { get => _windowState; set => SetProperty(ref _windowState, value); }
        private WindowState _windowState;
        public double LeftColumnPercent { get => _leftColumnPercent; set => SetProperty(ref _leftColumnPercent, value); }
        private double _leftColumnPercent;
        public double RightColumnPercent { get => _rightColumnPercent; set => SetProperty(ref _rightColumnPercent, value); }
        private double _rightColumnPercent;
        public Guid WindowId { get => _windowId; private set => SetProperty(ref _windowId, value); }
        private Guid _windowId;
        public bool IsHiddenFilesVisible { get => _isHiddenFilesVisible; set => SetProperty(ref _isHiddenFilesVisible, value); }
        private bool _isHiddenFilesVisible;
        public ISystemItem? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }
        private ISystemItem? _selectedItem;
        public ISystemItem? SelectedListItem { get => _selectedListItem; set => SetProperty(ref _selectedListItem, value); }
        private ISystemItem? _selectedListItem;
        public IReadOnlyList<ISystemItem> SelectedItemsSnapshot
        {
            get => _selectedItemsSnapshot;
            set
            {
                IReadOnlyList<ISystemItem> snapshot =
                    FileExplorerSelectionPolicy.CreateSnapshot(value);
                if(SetProperty(ref _selectedItemsSnapshot, snapshot))
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
        private IReadOnlyList<ISystemItem> _selectedItemsSnapshot =
            Array.Empty<ISystemItem>();
        public string CurrentPath { get => _currentPath; private set => SetProperty(ref _currentPath, value); }
        private string _currentPath = string.Empty;
        public string AddressText { get => _addressText; set => SetProperty(ref _addressText, value); }
        private string _addressText = string.Empty;
        public bool IsCurrentDirectoryUnavailable
        {
            get => _isCurrentDirectoryUnavailable;
            private set
            {
                if(SetProperty(ref _isCurrentDirectoryUnavailable, value))
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
        private bool _isCurrentDirectoryUnavailable;
        public FileExplorerNavigationErrorKind? LastNavigationError
        {
            get => _lastNavigationError;
            private set => SetProperty(ref _lastNavigationError, value);
        }
        private FileExplorerNavigationErrorKind? _lastNavigationError;
        public string NavigationErrorMessage
        {
            get => _navigationErrorMessage;
            private set => SetProperty(ref _navigationErrorMessage, value);
        }
        private string _navigationErrorMessage = string.Empty;
        public ReadOnlyObservableCollection<IDriveItem> GetDrives { get; private set; }
        private string _lastItemName;


        public FileExplorerModel(IFileManagerService? fileManagerService, IDriveManagerService? driveManagerService,
            IWindowManager? windowManager, IFileClipboardService fileClipboardService, IFolderPickerService folderPickerService, IMessageService messageService, IKeyProvider keyProvider, IFileSecurityService fileSecurityService, ISystemItemCreateService systemItemCreateService, IProgressDialogService progressDialogService, IFileOperationCoordinator fileOperationCoordinator, IWorkspaceFileOpenService fileOpenService, IFileLauncherService fileLauncherService, IDocumentSession documentSession, IPinnedDocumentService pinnedDocumentService, IRecentDocumentService? recentDocumentService = null)
        {
            WindowId = Guid.NewGuid();
            _fileManagerService = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
            _driveManagerService = driveManagerService ?? throw new ArgumentNullException(nameof(driveManagerService));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _fileClipboardService = fileClipboardService ?? throw new ArgumentNullException(nameof(fileClipboardService));
            _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            _fileSecurityService = fileSecurityService ?? throw new ArgumentNullException(nameof(fileSecurityService));
            _systemItemCreateService = systemItemCreateService ?? throw new ArgumentNullException(nameof(systemItemCreateService));
            _progressDialogService = progressDialogService ?? throw new ArgumentNullException(nameof(progressDialogService));
            _fileOperationCoordinator = fileOperationCoordinator ?? throw new ArgumentNullException(nameof(fileOperationCoordinator));
            _fileOpenService = fileOpenService ?? throw new ArgumentNullException(nameof(fileOpenService));
            _fileLauncherService = fileLauncherService ?? throw new ArgumentNullException(nameof(fileLauncherService));
            _documentSession = documentSession ?? throw new ArgumentNullException(nameof(documentSession));
            _pinnedDocumentService = pinnedDocumentService ?? throw new ArgumentNullException(nameof(pinnedDocumentService));
            _recentDocumentService = recentDocumentService;
            GetDrives = _driveManagerService.WritableDrives;
        }


        public bool CanExecute_BackCommand(object? obj) => _navigationHistory.CanGoBack;
        public bool CanExecute_ForwardCommand(object? obj) => _navigationHistory.CanGoForward;
        public bool CanExecute_UpCommand(object? obj) =>
            !string.IsNullOrWhiteSpace(CurrentPath) &&
            Path.GetDirectoryName(CurrentPath) is not null;
        public bool CanExecute_ApplyAddressCommand(object? obj) =>
            !string.IsNullOrWhiteSpace(AddressText);
        public bool CanExecute_RetryNavigationCommand(object? obj) =>
            IsCurrentDirectoryUnavailable &&
            !string.IsNullOrWhiteSpace(CurrentPath);
        public bool CanExecute_CurrentDocumentCommand(object? obj) =>
            !string.IsNullOrWhiteSpace(_documentSession.FilePath) &&
            Path.GetDirectoryName(_documentSession.FilePath) is not null;
        public bool CanExecute_OpenCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable &&
            FileExplorerSelectionPolicy.IsSingle(obj);
        public bool CanExecute_OpenWithCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable &&
            GetSingleSelection(obj) is IFileItem;
        public bool CanExecute_RevealInExplorerCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable &&
            FileExplorerSelectionPolicy.IsSingle(obj);
        public bool CanExecute_CopyPathCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable &&
            FileExplorerSelectionPolicy.NormalizeForOperation(obj).Count > 0;
        public bool CanExecute_CutCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable && CanExecuteMultiItemOperation(obj);
        public bool CanExecute_CopyCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable && CanExecuteMultiItemOperation(obj);
        public bool CanExecute_PasteCommand(object? obj)
        {
            if(!IsCurrentDirectoryUnavailable &&
               !string.IsNullOrEmpty(CurrentPath))
            {
                return _fileClipboardService.GetData().SourcePaths.Count > 0;
            }
            return false;
        }
        public bool CanExecute_DeleteCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable && CanExecuteMultiItemOperation(obj);
        public bool CanExecute_SortedCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
            obj is string name && !string.IsNullOrWhiteSpace(name) &&
            SelectedItem is IContainerSystemItem item && item.Children.Count > 1;
        }
        public bool CanExecute_EncryptingKeyCommand(object? obj)
        {
            return true;
        }
        public bool CanExecute_DecryptCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
                _keyProvider.HasKey && CanExecuteMultiItemOperation(obj);
        }
        public bool CanExecute_EncryptCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
                _keyProvider.HasKey && CanExecuteMultiItemOperation(obj);
        }
        public bool CanExecute_CreateFileCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
                SelectedItem is IContainerSystemItem;
        }
        public bool CanExecute_CreateDirectoryCommand(object? obj)                                                     
        {
            return !IsCurrentDirectoryUnavailable &&
                SelectedItem is IContainerSystemItem;
        }
        public bool CanExecute_RenameClickCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
                   GetSingleSelection(obj) is ISystemItem item &&
                   item is not IDriveItem &&
                   !item.IsEditing;
        }
        public bool CanExecute_RenameCommand(object? obj)
        {
            return obj is not null;
        }
        public bool CanExecute_MoveCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
                   CanExecuteMultiItemOperation(obj) &&
                   FileExplorerSelectionPolicy.NormalizeForOperation(obj)
                       .All(item => item.Parent is not null);
        }
        public bool CanExecute_DropCommand(object? obj)
        {
            if(obj is not FileDropRequest request ||
               request.SourcePaths.Count == 0 ||
               string.IsNullOrWhiteSpace(request.DestinationDirectory))
            {
                return false;
            }

            try
            {
                foreach(string sourcePath in request.SourcePaths)
                {
                    FileOperationCoordinator.ValidateDestination(
                        sourcePath,
                        request.DestinationDirectory,
                        Directory.Exists(GetNativePath(sourcePath)));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        public bool CanExecure_RefreshCommand(object? obj)
        {
            return obj is IContainerSystemItem ||
                   !string.IsNullOrWhiteSpace(CurrentPath);
        }
        public bool CanExecute_CancelRenameCommand(object? obj)
        {
            return obj is ISystemItem item && item.IsEditing;
        }
        public bool CanExecute_TreeViewItemSelectedCommand(object? obj)
        {
            return obj is not null;
        }
        public bool CanExecute_ListViewItemDoubleClickCommand(object? obj)
        {
            return FileExplorerSelectionPolicy.IsSingle(obj);
        }
        public bool CanExecute_ListViewSelectionChangedCommand(object? obj)
        {
            return obj is not null;
        }
        public bool CanExecute_WindowSizeChanged(object? obj)
        {
            return true;
        }


        public async void Execute_BackCommand(object? obj)
        {
            string? path = _navigationHistory.BackPath;
            if(path is not null)
                await NavigateAsync(path, FileExplorerNavigationMode.Back);
        }

        public async void Execute_ForwardCommand(object? obj)
        {
            string? path = _navigationHistory.ForwardPath;
            if(path is not null)
                await NavigateAsync(path, FileExplorerNavigationMode.Forward);
        }

        public async void Execute_UpCommand(object? obj)
        {
            string? parentPath = string.IsNullOrWhiteSpace(CurrentPath)
                ? null
                : Path.GetDirectoryName(CurrentPath);
            if(parentPath is not null)
            {
                await NavigateAsync(
                    parentPath,
                    FileExplorerNavigationMode.Standard);
            }
        }

        public async void Execute_ApplyAddressCommand(object? obj)
        {
            await NavigateAsync(
                AddressText,
                FileExplorerNavigationMode.Standard);
        }

        public void Execute_CancelAddressCommand(object? obj)
        {
            AddressText = CurrentPath;
        }

        public async void Execute_RetryNavigationCommand(object? obj)
        {
            if(!string.IsNullOrWhiteSpace(CurrentPath))
            {
                await NavigateAsync(
                    CurrentPath,
                    FileExplorerNavigationMode.Standard);
            }
        }

        public async void Execute_CurrentDocumentCommand(object? obj)
        {
            string? filePath = _documentSession.FilePath;
            string? directoryPath = string.IsNullOrWhiteSpace(filePath)
                ? null
                : Path.GetDirectoryName(filePath);
            if(filePath is null || directoryPath is null)
                return;

            bool navigated = await NavigateAsync(
                directoryPath,
                FileExplorerNavigationMode.Standard);
            if(!navigated || SelectedItem is not IContainerSystemItem container)
                return;

            SelectedListItem = container.Children.FirstOrDefault(item =>
                PathsEqual(item.FullPath, filePath));
        }

        public async void Execute_OpenCommand(object? obj)
        {
            ISystemItem? item = GetSingleSelection(obj);
            if(item is null)
                return;

            await OpenSelectionAsync(item);
        }

        public async void Execute_OpenWithCommand(object? obj)
        {
            if(GetSingleSelection(obj) is not IFileItem file)
                return;

            LaunchResult result = _fileLauncherService.Open(file.FullPath, "openas");
            if(!result.Success)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.FileOpenError"),
                    result.Error);
            }
        }

        public async void Execute_RevealInExplorerCommand(object? obj)
        {
            ISystemItem? item = GetSingleSelection(obj);
            if(item is null)
                return;

            LaunchResult result = _fileLauncherService.RevealInExplorer(item.FullPath);
            if(!result.Success)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.FileOpenError"),
                    result.Error);
            }
        }

        public async void Execute_CopyPathCommand(object? obj)
        {
            IReadOnlyList<ISystemItem> items =
                FileExplorerSelectionPolicy.NormalizeForOperation(obj);
            if(items.Count == 0)
                return;

            try
            {
                System.Windows.Clipboard.SetText(string.Join(
                    Environment.NewLine,
                    items.Select(item => item.FullPath)));
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.CopyPathError"),
                    ex.Message);
            }
        }

        public void Execute_CutCommand(object? obj)
        {
            IReadOnlyList<ISystemItem> items = GetOperationSelection(obj);
            if(items.Count == 0)
                return;

            _fileClipboardService.SetMove(items.Select(item => item.FullPath));
        }
        public void Execute_CopyCommand(object? obj)
        {
            IReadOnlyList<ISystemItem> items = GetOperationSelection(obj);
            if(items.Count == 0)
                return;

            _fileClipboardService.SetCopy(items.Select(item => item.FullPath));
        }
        public async void Execute_PasteCommand(object? obj)
        {
            ClipboardData clipboard = _fileClipboardService.GetData();
            if(!string.IsNullOrEmpty(CurrentPath) && !clipboard.IsEmpty)
            {
                try
                {
                    FileTransferKind operation = clipboard.Operation == ClipboardOperationKind.Copy
                        ? FileTransferKind.Copy
                        : FileTransferKind.Move;
                    FileOperationBatchResult result = await _fileOperationCoordinator.TransferAsync(
                        clipboard.SourcePaths,
                        CurrentPath,
                        operation);
                    if(result.Failure is not null)
                    {
                        await _messageService.ShowMessage(
                            LocalizationManager.GetString(
                                operation == FileTransferKind.Copy
                                    ? "Explorer.CopyError"
                                    : "Explorer.MoveError"),
                            result.Failure.ErrorMessage);
                    }

                    if(operation == FileTransferKind.Move && result.Success)
                        _fileClipboardService.Clear();
                    await RefreshOperationContainersAsync(
                        clipboard.SourcePaths,
                        CurrentPath,
                        CancellationToken.None);
                }
                catch(OperationCanceledException)
                {
                }
                catch(Exception ex)
                {
                    _ = await _messageService.ShowMessage(
                        LocalizationManager.GetString("Explorer.CopyError"),
                        ex.Message);
                }
            } else
            {
                throw new ArgumentException("Invalid argument for PasteCommand", nameof(obj));
            }
        }
        public async void Execute_DeleteCommand(object? obj)
        {
            IReadOnlyList<ISystemItem> items = GetOperationSelection(obj);
            if(items.Count == 0)
                return;

            try
            {
                FileOperationBatchResult result = await _fileOperationCoordinator.DeleteAsync(
                    items.Select(item => item.FullPath));
                if(result.Failure is not null)
                {
                    await _messageService.ShowMessage(
                        LocalizationManager.GetString("Explorer.DeleteError"),
                        result.Failure.ErrorMessage);
                }
                else if(result.Canceled && result.HasPartialChanges)
                {
                    await _messageService.ShowMessage(
                        LocalizationManager.GetString("Explorer.DeleteCanceledTitle"),
                        LocalizationManager.GetString("Explorer.DeleteCanceledPartial"));
                }

                await RefreshOperationContainersAsync(
                    items.Select(item => item.FullPath),
                    null,
                    CancellationToken.None);
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.DeleteError"),
                    ex.Message);
            }
        }
        public async void Execute_SortedCommand(object? obj)
        {
            if(obj is string name && !string.IsNullOrWhiteSpace(name) && SelectedItem is IContainerSystemItem container)
            {
                if(Enum.TryParse<SystemItemSortType>(name, ignoreCase: true, out SystemItemSortType result))
                {
                    var fileOperationResult = await container.SortingAsync(result);
                    if(fileOperationResult.Success)
                        return;
                    _ = await _messageService.ShowMessage(
                        LocalizationManager.GetString("Explorer.SortError"),
                        fileOperationResult.ErrorMessage);
                } else
                {
                    Console.WriteLine("Не удалось распознать");
                    _ = await _messageService.ShowMessage(
                        LocalizationManager.GetString("Explorer.SortError"),
                        LocalizationManager.GetString(
                            "Explorer.UnknownSortColumn"));
                }
            }
        }
        public void Execute_EncryptingKeyCommand(object? obj)
        {
            var id = _windowManager.CreateWindow<KeyInputWindow>();
            _windowManager.ShowWindowDialog(id);
        }
        public async void Execute_EncryptCommand(object? obj)
        {
            await ExecuteFileSecurityCommandAsync(obj, decrypt: false);

        }
        public async void Execute_DecryptCommand(object? obj)
        {
            await ExecuteFileSecurityCommandAsync(obj, decrypt: true);
        }
        public void Execute_CreateFileCommand(object? obj)
        {
            var id = _windowManager.CreateWindow<NewFileDialog>();
            _windowManager.ShowWindow(id);
            Execute_SortedCommand("Name");
        }
        public async void Execute_CreateDirectoryCommand(object? obj)
        {
            if(SelectedItem is not IContainerSystemItem container)
                return;

            var dialogId = _windowManager.CreateWindow<DirectoryNameDialog>();
            _windowManager.ShowWindowDialog(dialogId);

            string? directoryName = _windowManager.GetResult<string?>(dialogId)?.Trim();
            if(directoryName is null)
                return;

            if(!TryValidateDirectoryName(directoryName, out string validationError))
            {
                _ = await _messageService.ShowMessage(
                    LocalizationManager.GetString(
                        "Explorer.CreateDirectoryError"),
                    validationError);
                return;
            }

            string directoryPath = Path.Combine(container.FullPath, directoryName);
            if(Path.Exists(directoryPath))
            {
                _ = await _messageService.ShowMessage(
                    LocalizationManager.GetString(
                        "Explorer.CreateDirectoryError"),
                    LocalizationManager.Format(
                        "Explorer.ItemAlreadyExists",
                        directoryName));
                return;
            }

            FileOperationResult result = await _fileManagerService.CreateDirectoryAsync(container.FullPath, directoryName, CancellationToken.None);

            if(!result.Success)
            {
                _ = await _messageService.ShowMessage(
                    LocalizationManager.GetString(
                        "Explorer.CreateDirectoryError"),
                    result.ErrorMessage);
                return;
            }

            // После успешного создания на диске добавляем представление в текущий контейнер.
            var directoryItem = _systemItemCreateService.CreateDirectory(directoryPath, container);
            await container.AddChildAsync( [directoryItem], item => item.FullPath, CancellationToken.None);
            await container.SortingAsync( SystemItemSortType.Name, 0, CancellationToken.None);
        }
        public async void Execute_RenameClickCommand(object? obj)
        {
            if(GetSingleSelection(obj) is ISystemItem systemItem &&
               systemItem is not IDriveItem)
            {
                if(systemItem.Parent is IContainerSystemItem parent &&
                   !ReferenceEquals(SelectedItem, parent))
                {
                    bool navigated = await NavigateAsync(
                        parent.FullPath,
                        FileExplorerNavigationMode.Standard);
                    if(!navigated)
                        return;

                    SelectedListItem = systemItem;
                    SelectedItemsSnapshot =
                        FileExplorerSelectionPolicy.CreateSnapshot(systemItem);
                }

                systemItem.IsEditing = true;
                _lastItemName = systemItem.Name;
            }
        }
        public async void Execute_RenameCommand(object? obj)
        {
            if(obj is ISystemItem systemItem)
            {
                if(!systemItem.IsEditing)
                    return;
                if(string.IsNullOrWhiteSpace(systemItem.Name) || systemItem.FullPath.Equals(System.IO.Path.Combine(systemItem.RootDirectory, systemItem.Name), StringComparison.OrdinalIgnoreCase))
                {
                    systemItem.Name = _lastItemName;
                    return;
                }
                //выполняем переименование
                string oldPath = systemItem.FullPath;
                string? directory = Path.GetDirectoryName(oldPath);
                string? newPath = string.IsNullOrWhiteSpace(directory)
                    ? null
                    : Path.Combine(directory, systemItem.Name);
                var res = await _fileManagerService.RenameAsync(oldPath, systemItem.Name, CancellationToken.None);
                if(res.Success)
                {
                    systemItem.Parent = SelectedItem;
                    if(systemItem.Parent is IContainerSystemItem parentSystemItem)
                    {
                        res = await parentSystemItem.RenameChildAsync(systemItem, systemItem.Name, CancellationToken.None);
                    }
                    if(!res.Success)
                    {
                        _ = await _messageService.ShowMessage(
                            LocalizationManager.GetString(
                                "Explorer.RenameError"),
                            res.ErrorMessage);
                        systemItem.Name = _lastItemName;
                    }

                    if(res.Success && !string.IsNullOrWhiteSpace(newPath))
                        await SynchronizeRenamedDocumentAsync(oldPath, newPath);
                }
                systemItem.IsEditing = false;
            } else
            {
                throw new ArgumentException("Invalid argument for RenameCommand", nameof(obj));
            }
        }

        private async Task SynchronizeRenamedDocumentAsync(
            string oldPath,
            string newPath)
        {
            if(PathsEqual(_documentSession.FilePath, oldPath))
                _documentSession.Rename(newPath);

            try
            {
                await _pinnedDocumentService.UpdatePathAsync(
                    oldPath,
                    newPath,
                    CancellationToken.None);
                if(_recentDocumentService is not null)
                {
                    await _recentDocumentService.UpdatePathAsync(
                        oldPath,
                        newPath,
                        CancellationToken.None);
                }
            }
            catch(Exception exception)
            {
                Debug.WriteLine(exception);
                _ = await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.RenameError"),
                    LocalizationManager.GetString(
                        "DocumentLinks.RenameSyncFailed"));
            }
        }

        private static bool PathsEqual(string? left, string right)
        {
            if(string.IsNullOrWhiteSpace(left))
                return false;

            try
            {
                return string.Equals(
                    Path.GetFullPath(left),
                    Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch(Exception exception) when(
                exception is ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
        public async void Execute_MoveCommand(object? obj)
        {
            IReadOnlyList<ISystemItem> items = GetOperationSelection(obj);
            if(items.Count == 0 || !CanExecute_MoveCommand(items))
                return;

            try
            {
                string? destinationDirectory = await _folderPickerService.PickFolderAsync(
                    items[0].Parent?.FullPath ?? Path.GetDirectoryName(items[0].FullPath),
                    CancellationToken.None);
                if(string.IsNullOrWhiteSpace(destinationDirectory))
                    return;

                FileOperationBatchResult result = await _fileOperationCoordinator.TransferAsync(
                    items.Select(item => item.FullPath),
                    destinationDirectory,
                    FileTransferKind.Move);
                if(result.Failure is not null)
                {
                    await _messageService.ShowMessage(
                        LocalizationManager.GetString("Explorer.MoveError"),
                        result.Failure.ErrorMessage);
                }

                await RefreshOperationContainersAsync(
                    items.Select(item => item.FullPath),
                    destinationDirectory,
                    CancellationToken.None);
            } catch(OperationCanceledException)
            {
            } catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.MoveError"),
                    ex.Message);
            }
        }
        public async void Execute_RefreshCommand(object? obj)
        {
            if(IsCurrentDirectoryUnavailable)
            {
                Execute_RetryNavigationCommand(obj);
                return;
            }

            IContainerSystemItem? container = obj as IContainerSystemItem ??
                SelectedItem as IContainerSystemItem;
            if(container is null)
                return;

            try
            {
                await RefreshContainerAsync(container, CancellationToken.None);
                if(PathsEqual(container.FullPath, CurrentPath))
                    ClearNavigationFailure();
            } catch(OperationCanceledException)
            {
                SetNavigationFailure(
                    FileExplorerNavigationErrorKind.OperationCanceled,
                    false);
            } catch(Exception ex)
            {
                if(PathsEqual(container.FullPath, CurrentPath))
                {
                    await HandleNavigationFailureAsync(
                        CurrentPath,
                        FileExplorerNavigationErrorClassifier.Classify(
                            ex,
                            CurrentPath));
                }
                else
                {
                    await _messageService.ShowMessage(
                        LocalizationManager.GetString("Explorer.RefreshError"),
                        ex.Message);
                }
            }
        }
        public async void Execute_CancelRenameCommand(object? obj)
        {
            var id = await _messageService.ShowMessage(
                LocalizationManager.GetString("Explorer.CancelOperation"),
                LocalizationManager.Format(
                    "Explorer.RenameCanceledConfirmation",
                    Environment.NewLine),
                true);
            if(obj is ISystemItem systemItem && _messageService.ShowConfirmation(id))
            {
                systemItem.Name = _lastItemName;
                systemItem.IsEditing = false;
            }
        }
        public async void Execute_TreeViewItemSelectedCommand(object? obj)
        {
            if(obj is not IContainerSystemItem container)
                return;

            // Выбор узла не меняет IsExpanded: раскрытием управляет сам TreeView.
            await NavigateAsync(
                container.FullPath,
                FileExplorerNavigationMode.Standard);
        }
        public async void Execute_ListViewItemDoubleClickCommand(object? obj)
        {
            ISystemItem? item = GetSingleSelection(obj);
            if(item is not null)
                await OpenSelectionAsync(item);
        }

        public async Task<bool> NavigateAsync(
            string path,
            FileExplorerNavigationMode mode,
            CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(path))
            {
                await ShowNavigationErrorAsync(
                    path,
                    LocalizationManager.GetString(
                        "Explorer.DirectoryPathRequired"));
                return false;
            }

            string nativePath = GetNativePath(path);
            var lockTaken = false;
            try
            {
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _cancellationTokenSource.Token);
                CancellationToken token = linkedCancellation.Token;

                await _gate.WaitAsync(token);
                lockTaken = true;

                string targetPath = NormalizePath(nativePath);
                IContainerSystemItem container = await ResolveDirectoryContainerAsync(
                    targetPath,
                    token);
                if(container.IsLoaded)
                {
                    // Повторная навигация обязана проверить каталог на диске,
                    // иначе ранее загруженный снимок маскирует его исчезновение.
                    await RefreshContainerAsync(container, token);
                }
                else
                {
                    FileOperationResult result = await ContainerLoad(container, token);
                    if(!result.Success)
                        throw new IOException(result.ErrorMessage);
                }

                string? previousPath = string.IsNullOrWhiteSpace(CurrentPath)
                    ? null
                    : CurrentPath;
                _navigationHistory.Commit(previousPath, targetPath, mode);

                if(SelectedItem is IContainerSystemItem previousContainer &&
                   !ReferenceEquals(previousContainer, container))
                {
                    previousContainer.IsSelected = false;
                }

                SelectedItem = container;
                SelectedListItem = null;
                SelectedItemsSnapshot = Array.Empty<ISystemItem>();
                CurrentPath = targetPath;
                AddressText = targetPath;
                ClearNavigationFailure();
                container.IsSelected = true;
                container.IsExpanded = true;
                container.IsLoaded = true;
                ObserveCurrentContainerAvailability(container);
                Properties.Settings.Default.LastDirectory_FileExplorer = targetPath;
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                return true;
            }
            catch(OperationCanceledException)
            {
                if(!IsCurrentDirectoryUnavailable)
                {
                    SetNavigationFailure(
                        FileExplorerNavigationErrorKind.OperationCanceled,
                        false);
                }
                if(cancellationToken.IsCancellationRequested)
                    throw;
                return false;
            }
            catch(Exception ex)
            {
                await HandleNavigationFailureAsync(
                    nativePath,
                    FileExplorerNavigationErrorClassifier.Classify(
                        ex,
                        nativePath));
                return false;
            }
            finally
            {
                if(lockTaken)
                    _gate.Release();
            }
        }

        public async Task RestoreLastDirectoryAsync(
            CancellationToken cancellationToken = default)
        {
            string savedPath = Properties.Settings.Default.LastDirectory_FileExplorer;
            if(!string.IsNullOrWhiteSpace(savedPath) &&
               await NavigateAsync(
                   savedPath,
                   FileExplorerNavigationMode.Restore,
                   cancellationToken))
            {
                return;
            }

            string? firstDrivePath = GetDrives.FirstOrDefault()?.FullPath;
            if(!string.IsNullOrWhiteSpace(firstDrivePath))
            {
                await NavigateAsync(
                    firstDrivePath,
                    FileExplorerNavigationMode.Restore,
                    cancellationToken);
            }
        }

        private async Task<IContainerSystemItem> ResolveDirectoryContainerAsync(
            string path,
            CancellationToken cancellationToken)
        {
            string fullPath = Path.GetFullPath(path);
            string rootPath = Path.GetPathRoot(fullPath)
                ?? throw new DirectoryNotFoundException(
                    LocalizationManager.Format(
                        "Explorer.RootPathNotFound",
                        path));

            IContainerSystemItem root = GetDrives
                .OfType<IContainerSystemItem>()
                .FirstOrDefault(item =>
                    string.Equals(
                        NormalizePath(item.FullPath),
                        NormalizePath(rootPath),
                        StringComparison.OrdinalIgnoreCase))
                ?? throw new DirectoryNotFoundException(rootPath);

            string relativePath = Path.GetRelativePath(rootPath, fullPath);
            if(relativePath == ".")
                return root;

            IContainerSystemItem current = root;
            string currentPath = rootPath;
            foreach(string segment in relativePath.Split(
                new[]
                {
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                },
                StringSplitOptions.RemoveEmptyEntries))
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileOperationResult loadResult = await ContainerLoad(
                    current,
                    cancellationToken);
                if(!loadResult.Success)
                    throw new IOException(loadResult.ErrorMessage);

                current.IsExpanded = true;
                currentPath = Path.Combine(currentPath, segment);
                IContainerSystemItem? existing = current.Children
                    .OfType<IContainerSystemItem>()
                    .FirstOrDefault(item =>
                        string.Equals(
                            NormalizePath(item.FullPath),
                            NormalizePath(currentPath),
                            StringComparison.OrdinalIgnoreCase));

                if(existing is null)
                {
                    existing = _systemItemCreateService.CreateDirectory(
                        currentPath,
                        current);
                    FileOperationResult addResult = await current.AddChildAsync(
                        [existing],
                        item => item.FullPath,
                        cancellationToken);
                    if(!addResult.Success)
                    {
                        // Наблюдатель мог добавить тот же каталог между
                        // поиском и AddChildAsync — используем его экземпляр.
                        existing = current.Children
                            .OfType<IContainerSystemItem>()
                            .FirstOrDefault(item =>
                                string.Equals(
                                    NormalizePath(item.FullPath),
                                    NormalizePath(currentPath),
                                    StringComparison.OrdinalIgnoreCase))
                            ?? throw new IOException(addResult.ErrorMessage);
                    }
                }

                current = existing;
            }

            return current;
        }

        private Task<Guid> ShowNavigationErrorAsync(
            string path,
            string error) =>
            _messageService.ShowMessage(
                LocalizationManager.GetString(
                    "Explorer.OpenDirectoryError"),
                LocalizationManager.Format(
                    "Explorer.OpenDirectoryFailed",
                    path,
                    Environment.NewLine,
                    error));

        private async Task HandleNavigationFailureAsync(
            string path,
            FileExplorerNavigationErrorKind kind)
        {
            bool affectsCurrentDirectory =
                !string.IsNullOrWhiteSpace(CurrentPath) &&
                PathsEqual(path, CurrentPath) &&
                kind != FileExplorerNavigationErrorKind.OperationCanceled;
            string errorMessage = GetNavigationErrorMessage(kind);
            if(affectsCurrentDirectory || !IsCurrentDirectoryUnavailable)
                SetNavigationFailure(kind, affectsCurrentDirectory);

            if(!affectsCurrentDirectory)
                await ShowNavigationErrorAsync(path, errorMessage);
        }

        private void SetNavigationFailure(
            FileExplorerNavigationErrorKind kind,
            bool currentDirectoryUnavailable)
        {
            LastNavigationError = kind;
            NavigationErrorMessage = GetNavigationErrorMessage(kind);
            if(currentDirectoryUnavailable)
            {
                IsCurrentDirectoryUnavailable = true;
                SelectedListItem = null;
                SelectedItemsSnapshot = Array.Empty<ISystemItem>();
            }
        }

        private static string GetNavigationErrorMessage(
            FileExplorerNavigationErrorKind kind) =>
            LocalizationManager.GetString(
                kind switch
                {
                    FileExplorerNavigationErrorKind.AccessDenied =>
                        "Explorer.Navigation.AccessDenied",
                    FileExplorerNavigationErrorKind.DriveNotReady =>
                        "Explorer.Navigation.DriveNotReady",
                    FileExplorerNavigationErrorKind.NetworkResourceUnavailable =>
                        "Explorer.Navigation.NetworkUnavailable",
                    FileExplorerNavigationErrorKind.OperationCanceled =>
                        "Explorer.Navigation.OperationCanceled",
                    _ => "Explorer.Navigation.DirectoryNotFound"
                });

        private void ClearNavigationFailure()
        {
            LastNavigationError = null;
            NavigationErrorMessage = string.Empty;
            IsCurrentDirectoryUnavailable = false;
        }

        private void ObserveCurrentContainerAvailability(
            IContainerSystemItem container)
        {
            if(_currentContainerAvailabilitySource is not null)
            {
                _currentContainerAvailabilitySource.CollectionChanged -=
                    CurrentContainerAvailabilitySource_CollectionChanged;
            }

            _currentContainerAvailabilitySource = container.Parent is
                IContainerSystemItem parent
                    ? (INotifyCollectionChanged)parent.Children
                    : (INotifyCollectionChanged)GetDrives;
            _currentContainerAvailabilitySource.CollectionChanged +=
                CurrentContainerAvailabilitySource_CollectionChanged;
        }

        private void CurrentContainerAvailabilitySource_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            if(SelectedItem is not IContainerSystemItem current ||
               string.IsNullOrWhiteSpace(CurrentPath))
            {
                return;
            }

            IEnumerable<ISystemItem> siblings = current.Parent is
                IContainerSystemItem parent
                    ? parent.Children
                    : GetDrives.Cast<ISystemItem>();
            if(siblings.Any(item => PathsEqual(item.FullPath, CurrentPath)))
                return;

            SetNavigationFailure(
                FileExplorerNavigationErrorKind.DirectoryNotFound,
                true);
        }

        private async Task OpenSelectionAsync(ISystemItem item)
        {
            try
            {
                switch(item)
                {
                    case IContainerSystemItem container:
                        await NavigateAsync(
                            container.FullPath,
                            FileExplorerNavigationMode.Standard);
                        break;
                    case IFileItem file when !file.IsEditing:
                        await OpenFileAsync(file, _cancellationTokenSource.Token);
                        break;
                }
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.FileOpenError"),
                    LocalizationManager.Format(
                        "Explorer.FileOpenFailed",
                        item.Name,
                        Environment.NewLine,
                        ex.Message));
            }
        }

        private async Task OpenFileAsync(IFileItem file, CancellationToken cancellationToken)
        {
            WorkspaceFileOpenResult result = await _fileOpenService.OpenAsync(
                file.FullPath,
                cancellationToken);
            if(result.Cancelled)
                return;
            if(!result.Success)
            {
                _ = await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.FileOpenError"),
                    result.Error ?? LocalizationManager.Format(
                        "Explorer.OpenFileDefaultFailed",
                        file.Name));
                return;
            }

            _windowManager.CloseWindow(WindowId);
        }
        public void Execute_ListViewSelectionChangedCommand(object? obj)
        {
        }
        public void Execute_WindowSizeChanged(object? obj)
        {
            OnPropertyChanged([nameof(LeftColumnPercent), nameof(RightColumnPercent)]);
        }
        public bool CanExecute_Close(object? obj)
        {
            return _windowManager.IsWindowOpen(WindowId);
        }
        public void Execute_Close(object? obj)
        {
            _cancellationTokenSource.Cancel();
            _windowManager.CloseWindow(WindowId);
        }
        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
            double savedHeight = Properties.Settings.Default.WindowHeight_FileExplorer;
            double savedWidth = Properties.Settings.Default.WindowWidth_FileExplorer;
            if(WindowLayoutDefaults.IsLegacyExplorerSize(savedWidth, savedHeight))
            {
                Rect placement = WindowLayoutDefaults.CreateExplorer(SystemParameters.WorkArea);
                WindowHeight = placement.Height;
                WindowWidth = placement.Width;
                WindowLeft = placement.Left;
                WindowTop = placement.Top;
            }
            else
            {
                WindowHeight = savedHeight;
                WindowWidth = savedWidth;
                WindowLeft = Properties.Settings.Default.WindowLeft_FileExplorer;
                WindowTop = Properties.Settings.Default.WindowTop_FileExplorer;
            }
            WindowState = Properties.Settings.Default.WindowState_FileExplorer;
            RightColumnPercent = Properties.Settings.Default.RightColumnPercent_FileExplorer;
            LeftColumnPercent = Properties.Settings.Default.LeftColumnPercent_FileExplorer;
            IsHiddenFilesVisible = Properties.Settings.Default.IsHiddenFilesVisible_FileExplorer;
        }
        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
            Properties.Settings.Default.WindowHeight_FileExplorer = WindowHeight;
            Properties.Settings.Default.WindowLeft_FileExplorer = WindowLeft;
            Properties.Settings.Default.WindowTop_FileExplorer = WindowTop;
            Properties.Settings.Default.WindowWidth_FileExplorer = WindowWidth;
            Properties.Settings.Default.WindowState_FileExplorer = WindowState;
            Properties.Settings.Default.RightColumnPercent_FileExplorer = RightColumnPercent;
            Properties.Settings.Default.LeftColumnPercent_FileExplorer = LeftColumnPercent;
            Properties.Settings.Default.IsHiddenFilesVisible_FileExplorer = IsHiddenFilesVisible;
            Properties.Settings.Default.Save();
        }
        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        public void Execute_Closed(object? obj)
        {
            if(_currentContainerAvailabilitySource is not null)
            {
                _currentContainerAvailabilitySource.CollectionChanged -=
                    CurrentContainerAvailabilitySource_CollectionChanged;
                _currentContainerAvailabilitySource = null;
            }
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }


        private async Task<FileOperationResult> ContainerLoad(IContainerSystemItem container, CancellationToken token)
        {
            if(container.IsLoaded)
                return FileOperationResult.Ok();

            var children = await _fileManagerService.BrowseAsync(
                container.FullPath,
                null,
                token,
                IsHiddenFilesVisible);

            token.ThrowIfCancellationRequested();
            foreach(var child in children)
            {
                // Связываем элементы с фактическим узлом дерева, а не с временным представлением провайдера.
                child.Parent = container;
            }

            FileOperationResult result = await container.AddChildAsync(
                children,
                item => item.FullPath,
                token);
            if(result.Success)
                return result;

            // FileSystemWatcher может успеть добавить элементы между BrowseAsync
            // и пакетным добавлением. Совпавший итоговый снимок считается успехом.
            var currentPaths = new HashSet<string>(
                container.Children.Select(item => NormalizePath(item.FullPath)),
                StringComparer.OrdinalIgnoreCase);
            return children.All(item =>
                currentPaths.Contains(NormalizePath(item.FullPath)))
                    ? FileOperationResult.Ok()
                    : result;
        }

        private static bool TryValidateDirectoryName(string name, out string error)
        {
            error = string.Empty;

            if(string.IsNullOrWhiteSpace(name))
            {
                error = LocalizationManager.GetString(
                    "Explorer.DirectoryNameEmpty");
                return false;
            }

            if(name is "." or ".." ||
               name.EndsWith(' ') ||
               name.EndsWith('.') ||
               name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
               name.Contains(Path.DirectorySeparatorChar) ||
               name.Contains(Path.AltDirectorySeparatorChar))
            {
                error = LocalizationManager.GetString(
                    "Explorer.DirectoryNameInvalid");
                return false;
            }

            string baseName = name.Split('.')[0];
            string[] reservedNames =
            [
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            ];

            if(reservedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase))
            {
                error = LocalizationManager.Format(
                    "Explorer.DirectoryNameReserved",
                    name);
                return false;
            }

            return true;
        }

        private string? GetNewFilePath(string sourcePath)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = Path.GetDirectoryName(sourcePath),
                FileName = Path.GetFileNameWithoutExtension(sourcePath) +
                    "_" + LocalizationManager.GetString(
                        "Explorer.EncryptedSuffix"),
                DefaultExt = ".cbook",
                AddExtension = true,
                Filter = LocalizationManager.GetString(
                    "Explorer.CryptoBookFilesFilter"),
                FilterIndex = 1,
                OverwritePrompt = true,
                CheckPathExists = true,
                ValidateNames = true,
                Title = LocalizationManager.GetString(
                    "Explorer.SaveEncryptedTitle")
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private async Task ExecuteFileSecurityCommandAsync(object? obj, bool decrypt)
        {
            string errorTitle = LocalizationManager.GetString(
                decrypt
                    ? "Explorer.DecryptionError"
                    : "Explorer.EncryptionError");

            try
            {
                if(FileExplorerSelectionPolicy.ContainsDrive(obj))
                {
                    await _messageService.ShowMessage(
                        errorTitle,
                        LocalizationManager.GetString(
                            "Explorer.WholeDriveNotAllowed"));
                    return;
                }

                IReadOnlyList<ISystemItem> items = GetOperationSelection(obj);
                if(items.Count == 0)
                    return;

                var plans = new List<FileSecurityPlan>(items.Count);
                foreach(ISystemItem systemItem in items)
                {
                    if(systemItem is not IFileItem && systemItem is not IDirectoryItem)
                        return;

                    string sourcePath = systemItem.FullPath;
                    if(string.IsNullOrWhiteSpace(sourcePath) || !Path.Exists(sourcePath))
                    {
                        await _messageService.ShowMessage(
                            errorTitle,
                            LocalizationManager.GetString(
                                "Explorer.ItemDoesNotExist"));
                        return;
                    }

                    // Для групповой операции Save As неоднозначен, поэтому заменяем каждый источник.
                    (EncryptionTargetMode mode, string? targetPath) = items.Count > 1
                        ? (EncryptionTargetMode.ReplaceSource, sourcePath)
                        : ResolveFileSecurityTarget(systemItem, sourcePath, decrypt);

                    if(mode == EncryptionTargetMode.Cancels ||
                       string.IsNullOrWhiteSpace(targetPath))
                    {
                        return;
                    }

                    if(mode == EncryptionTargetMode.ReplaceSource &&
                       !await ConfirmSourceReplacementAsync(systemItem, sourcePath, decrypt))
                    {
                        return;
                    }

                    plans.Add(new FileSecurityPlan(systemItem, mode, targetPath));
                }

                IReadOnlyList<FileOperationResult> results = await _progressDialogService.RunAsync(
                    LocalizationManager.GetString(
                        decrypt
                            ? "Explorer.Decryption"
                            : "Explorer.Encryption"),
                    async (progress, token) =>
                    {
                        var operationResults = new List<FileOperationResult>(plans.Count);
                        for(int index = 0; index < plans.Count; index++)
                        {
                            FileSecurityPlan plan = plans[index];
                            var itemProgress = new BatchItemProgressReporter(
                                progress,
                                index,
                                plans.Count,
                                plan.Item.FullPath);
                            operationResults.Add(decrypt
                                ? await _fileSecurityService.DecryptAsync(
                                    plan.Item,
                                    plan.TargetPath,
                                    plan.Mode,
                                    itemProgress,
                                    token)
                                : await _fileSecurityService.EncryptAsync(
                                    plan.Item,
                                    plan.TargetPath,
                                    plan.Mode,
                                    itemProgress,
                                    token));
                            if(!operationResults[^1].Success)
                                break;
                        }

                        return (IReadOnlyList<FileOperationResult>)operationResults;
                    });

                FileOperationResult? failure = results.FirstOrDefault(result => !result.Success);
                if(failure is not null)
                {
                    await _messageService.ShowMessage(errorTitle, failure.ErrorMessage);
                    return;
                }

                try
                {
                    // Синхронизация нужна, если FileSystemWatcher пропустил быструю замену.
                    foreach(FileSecurityPlan plan in plans)
                    {
                        await RefreshAffectedContainersAsync(
                            plan.Item,
                            plan.TargetPath,
                            CancellationToken.None);
                    }
                } catch(Exception ex)
                {
                    await _messageService.ShowMessage(
                        LocalizationManager.GetString(
                            "Explorer.RefreshError"),
                        LocalizationManager.Format(
                            "Explorer.RefreshAfterOperationFailed",
                            Environment.NewLine,
                            ex.Message));
                }
            } catch(OperationCanceledException)
            {
                // Отмена пользователем является штатным завершением операции.
            } catch(Exception ex)
            {
                await _messageService.ShowMessage(errorTitle, ex.Message);
            }
        }

        private (EncryptionTargetMode Mode, string? TargetPath) ResolveFileSecurityTarget( ISystemItem systemItem, string sourcePath, bool decrypt)
        {
            // Для каталога текущий интерфейс поддерживает только замену файлов на месте.
            if(systemItem is IDirectoryItem)
                return (EncryptionTargetMode.ReplaceSource, sourcePath);

            var context = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?> { ["path"] = systemItem });

            Guid windowId = _windowManager.CreateWindow<EncryptionModeWindow>(context);
            _windowManager.ShowWindowDialog(windowId);

            EncryptionTargetMode mode =
                _windowManager.GetResult<EncryptionTargetMode>(windowId);
            if(mode == EncryptionTargetMode.Cancels)
                return (mode, null);

            string? targetPath = mode switch
            {
                EncryptionTargetMode.ReplaceSource => sourcePath,
                EncryptionTargetMode.SaveAs when decrypt => GetNewDecryptedFilePath(sourcePath),
                EncryptionTargetMode.SaveAs => GetNewFilePath(sourcePath),
                _ => null
            };

            if(!string.IsNullOrWhiteSpace(targetPath))
            {
                Settings.Default.EncryptionTargetMode = mode;
                Settings.Default.Save();
            }

            return (mode, targetPath);
        }

        private async Task<bool> ConfirmSourceReplacementAsync( ISystemItem systemItem, string sourcePath, bool decrypt)
        {
            bool isDirectory = systemItem is IDirectoryItem;
            string replacement = LocalizationManager.GetString(
                decrypt
                    ? isDirectory
                        ? "Explorer.DecryptedCopiesAdjective"
                        : "Explorer.DecryptedAdjective"
                    : isDirectory
                        ? "Explorer.EncryptedCopiesAdjective"
                        : "Explorer.EncryptedAdjective");
            string warning = LocalizationManager.Format(
                    isDirectory
                        ? "Explorer.OverwriteDirectoryPrompt"
                        : "Explorer.OverwriteFilePrompt",
                    replacement) +
                Environment.NewLine +
                Environment.NewLine +
                sourcePath;
            if(!decrypt)
            {
                warning += Environment.NewLine +
                    Environment.NewLine +
                    LocalizationManager.GetString(
                        "Explorer.EncryptionWarning");
            }

            Guid messageId = await _messageService.ShowMessage(
                LocalizationManager.GetString("Explorer.OverwriteTitle"),
                warning,
                true);
            return _messageService.ShowConfirmation(messageId);
        }

        private async Task RefreshAffectedContainersAsync( ISystemItem source, string targetPath, CancellationToken token)
        {
            var affectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddAffectedContainerPaths(affectedPaths, source.FullPath, source is IDirectoryItem);
            AddAffectedContainerPaths(affectedPaths, targetPath, source is IDirectoryItem);

            var containers = EnumerateLoadedContainers()
                .Where(container => affectedPaths.Contains(NormalizePath(container.FullPath)))
                .Distinct()
                .ToList();

            foreach(var container in containers)
            {
                token.ThrowIfCancellationRequested();
                await RefreshContainerAsync(container, token);
            }
        }

        private async Task RefreshContainerAsync( IContainerSystemItem container, CancellationToken token)
        {
            // Полный снимок устраняет пропущенные события FileSystemWatcher.
            var children = await _fileManagerService.BrowseAsync(
                container.FullPath,
                null,
                token,
                IsHiddenFilesVisible);

            foreach(var child in children)
            {
                child.Parent = container;
            }

            await container.SyncCollectionsAsync(
                children,
                item => item.FullPath,
                UpdateSystemItem,
                token);

            container.IsLoaded = true;
        }

        private IEnumerable<IContainerSystemItem> EnumerateLoadedContainers()
        {
            var pending = new Stack<IContainerSystemItem>(
                GetDrives.OfType<IContainerSystemItem>().Reverse());
            var visited = new HashSet<IContainerSystemItem>();

            while(pending.Count > 0)
            {
                var container = pending.Pop();
                if(!visited.Add(container))
                    continue;

                if(container.IsLoaded)
                    yield return container;

                foreach(var child in container.Children.OfType<IContainerSystemItem>().Reverse())
                {
                    pending.Push(child);
                }
            }
        }

        private async Task RefreshOperationContainersAsync(
            IEnumerable<string> sourcePaths,
            string? destinationDirectory,
            CancellationToken token)
        {
            var affectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string sourcePath in sourcePaths)
            {
                string? parentPath = Path.GetDirectoryName(GetNativePath(sourcePath));
                if(!string.IsNullOrWhiteSpace(parentPath))
                    affectedPaths.Add(NormalizePath(parentPath));
            }

            if(!string.IsNullOrWhiteSpace(destinationDirectory))
                affectedPaths.Add(NormalizePath(GetNativePath(destinationDirectory)));

            foreach(IContainerSystemItem container in EnumerateLoadedContainers()
                .Where(container => affectedPaths.Contains(
                    NormalizePath(GetNativePath(container.FullPath))))
                .Distinct())
            {
                await RefreshContainerAsync(container, token);
            }
        }

        public async void Execute_DropCommand(object? obj)
        {
            if(obj is not FileDropRequest request)
                return;

            try
            {
                FileOperationBatchResult result = await _fileOperationCoordinator.TransferAsync(
                    request.SourcePaths,
                    request.DestinationDirectory,
                    request.Operation);
                if(result.Failure is not null)
                {
                    await _messageService.ShowMessage(
                        LocalizationManager.GetString(
                            request.Operation == FileTransferKind.Copy
                                ? "Explorer.CopyError"
                                : "Explorer.MoveError"),
                        result.Failure.ErrorMessage);
                }

                await RefreshOperationContainersAsync(
                    request.SourcePaths,
                    request.DestinationDirectory,
                    CancellationToken.None);
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.FileOperationError"),
                    ex.Message);
            }
        }

        private static void AddAffectedContainerPaths(
            ISet<string> paths,
            string itemPath,
            bool isDirectory)
        {
            if(string.IsNullOrWhiteSpace(itemPath))
                return;

            if(isDirectory)
                paths.Add(NormalizePath(itemPath));

            string? parentPath = Path.GetDirectoryName(itemPath);
            if(!string.IsNullOrWhiteSpace(parentPath))
                paths.Add(NormalizePath(parentPath));
        }

        private static string NormalizePath(string path)
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        private static string GetNativePath(string path)
        {
            int separatorIndex = path.IndexOf("://", StringComparison.Ordinal);
            return separatorIndex > 0 ? path[(separatorIndex + 3)..] : path;
        }

        private static ISystemItem? GetSingleSelection(object? selection)
        {
            IReadOnlyList<ISystemItem> snapshot =
                FileExplorerSelectionPolicy.CreateSnapshot(selection);
            return snapshot.Count == 1 ? snapshot[0] : null;
        }

        private static bool CanExecuteMultiItemOperation(object? selection) =>
            !FileExplorerSelectionPolicy.ContainsDrive(selection) &&
            FileExplorerSelectionPolicy.NormalizeForOperation(selection).Count > 0;

        private static IReadOnlyList<ISystemItem> GetOperationSelection(
            object? selection) =>
            FileExplorerSelectionPolicy.ContainsDrive(selection)
                ? Array.Empty<ISystemItem>()
                : FileExplorerSelectionPolicy.NormalizeForOperation(selection);

        private static void UpdateSystemItem(ISystemItem existing, ISystemItem incoming)
        {
            existing.Name = incoming.Name;
            existing.FullPath = incoming.FullPath;
            existing.RootDirectory = incoming.RootDirectory;
            existing.Size = incoming.Size;
            existing.LastWriteTimeUtc = incoming.LastWriteTimeUtc;
            existing.Parent = incoming.Parent;

            if(existing is IFileItem existingFile && incoming is IFileItem incomingFile)
            {
                existingFile.Extension = incomingFile.Extension;
                existingFile.IsHidden = incomingFile.IsHidden;
                existingFile.IsReadOnly = incomingFile.IsReadOnly;
            }
        }

        private string? GetNewDecryptedFilePath(string sourcePath)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = Path.GetDirectoryName(sourcePath),
                FileName = Path.GetFileNameWithoutExtension(sourcePath) +
                    "_" + LocalizationManager.GetString(
                        "Explorer.DecryptedSuffix"),
                AddExtension = false,
                Filter = LocalizationManager.GetString(
                    "Explorer.AllFilesFilter"),
                FilterIndex = 1,
                OverwritePrompt = true,
                CheckPathExists = true,
                ValidateNames = true,
                Title = LocalizationManager.GetString(
                    "Explorer.SaveDecryptedTitle")
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private readonly record struct FileSecurityPlan(
            ISystemItem Item,
            EncryptionTargetMode Mode,
            string TargetPath);

        private sealed class BatchItemProgressReporter: IProgressReporter
        {
            private readonly IProgressReporter parent;
            private readonly int completedItems;
            private readonly int totalItems;
            private readonly string itemPath;

            public BatchItemProgressReporter(
                IProgressReporter parent,
                int completedItems,
                int totalItems,
                string itemPath)
            {
                this.parent = parent;
                this.completedItems = completedItems;
                this.totalItems = Math.Max(1, totalItems);
                this.itemPath = itemPath;
            }

            public void Report(double? value, string? currentInfo = null)
            {
                double? aggregate = value.HasValue
                    ? (completedItems + Math.Clamp(value.Value, 0d, 1d)) / totalItems
                    : null;
                parent.Report(aggregate, currentInfo ?? itemPath);
            }
        }


    }
}
