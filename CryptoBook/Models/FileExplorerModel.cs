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
        private readonly IStorageFacade _storage;
        private readonly ILocalFileSystemFacade _localFiles;
        private readonly IWindowManager _windowManager;
        private readonly IDriveManagerService _driveManagerService;
        private readonly IFileClipboardService _fileClipboardService;
        private readonly IFolderPickerService _folderPickerService;
        private readonly IMessageService _messageService;
        private readonly IKeyProvider _keyProvider;
        private readonly IFileSecurityService _fileSecurityService;
        private readonly IDecryptionExportService _decryptionExportService;
        private readonly ISystemItemCreateService _systemItemCreateService;
        private readonly IProgressDialogService _progressDialogService;
        private readonly IViewRenderSynchronizationService _viewRenderSynchronizationService;
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
        public bool IsFlatViewEnabled
        {
            get => _isFlatViewEnabled;
            set => SetProperty(ref _isFlatViewEnabled, value);
        }
        private bool _isFlatViewEnabled;
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
        public string CurrentDisplayPath => GetDisplayPath(CurrentPath);
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
            IWindowManager? windowManager, IFileClipboardService fileClipboardService, IFolderPickerService folderPickerService, IMessageService messageService, IKeyProvider keyProvider, IFileSecurityService fileSecurityService, IDecryptionExportService decryptionExportService, ISystemItemCreateService systemItemCreateService, IProgressDialogService progressDialogService, IViewRenderSynchronizationService viewRenderSynchronizationService, IFileOperationCoordinator fileOperationCoordinator, IWorkspaceFileOpenService fileOpenService, IFileLauncherService fileLauncherService, IDocumentSession documentSession, IPinnedDocumentService pinnedDocumentService, IRecentDocumentService? recentDocumentService = null, IStorageFacade? storageFacade = null, ILocalFileSystemFacade? localFileSystem = null)
        {
            WindowId = Guid.NewGuid();
            _fileManagerService = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
            _storage = storageFacade ?? new StorageFacade([new LocalStorageProvider()]);
            _localFiles = localFileSystem ?? new LocalFileSystemFacade();
            _driveManagerService = driveManagerService ?? throw new ArgumentNullException(nameof(driveManagerService));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _fileClipboardService = fileClipboardService ?? throw new ArgumentNullException(nameof(fileClipboardService));
            _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            _fileSecurityService = fileSecurityService ?? throw new ArgumentNullException(nameof(fileSecurityService));
            _decryptionExportService = decryptionExportService ?? throw new ArgumentNullException(nameof(decryptionExportService));
            _systemItemCreateService = systemItemCreateService ?? throw new ArgumentNullException(nameof(systemItemCreateService));
            _progressDialogService = progressDialogService ?? throw new ArgumentNullException(nameof(progressDialogService));
            _viewRenderSynchronizationService = viewRenderSynchronizationService ?? throw new ArgumentNullException(nameof(viewRenderSynchronizationService));
            _fileOperationCoordinator = fileOperationCoordinator ?? throw new ArgumentNullException(nameof(fileOperationCoordinator));
            _fileOpenService = fileOpenService ?? throw new ArgumentNullException(nameof(fileOpenService));
            _fileLauncherService = fileLauncherService ?? throw new ArgumentNullException(nameof(fileLauncherService));
            _documentSession = documentSession ?? throw new ArgumentNullException(nameof(documentSession));
            _pinnedDocumentService = pinnedDocumentService ?? throw new ArgumentNullException(nameof(pinnedDocumentService));
            _recentDocumentService = recentDocumentService;
            CleanupOrphanedDecryptionDirectories(
                GetDecryptionTemporaryRoot());
            GetDrives = _driveManagerService.WritableDrives;
        }


        public bool CanExecute_BackCommand(object? obj) => _navigationHistory.CanGoBack;
        public bool CanExecute_ForwardCommand(object? obj) => _navigationHistory.CanGoForward;
        public bool CanExecute_UpCommand(object? obj) =>
            !string.IsNullOrWhiteSpace(CurrentPath) &&
            _storage.GetParent(_storage.Resolve(CurrentPath)) is not null;
        public bool CanExecute_ApplyAddressCommand(object? obj) =>
            !string.IsNullOrWhiteSpace(AddressText);
        public bool CanExecute_RetryNavigationCommand(object? obj) =>
            IsCurrentDirectoryUnavailable &&
            !string.IsNullOrWhiteSpace(CurrentPath);
        public bool CanExecute_CurrentDocumentCommand(object? obj) =>
            !string.IsNullOrWhiteSpace(_documentSession.FilePath) &&
            _storage.GetParent(_storage.Resolve(_documentSession.FilePath)) is not null;
        public bool CanExecute_OpenCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable &&
            GetSingleSelection(obj) is ISystemItem item &&
            (item is IContainerSystemItem
                ? Supports(item, StorageProviderCapabilities.Browse)
                : Supports(item, StorageProviderCapabilities.OpenExternally));
        public bool CanExecute_OpenWithCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable &&
            GetSingleSelection(obj) is IFileItem item &&
            Supports(item, StorageProviderCapabilities.OpenExternally);
        public bool CanExecute_RevealInExplorerCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable &&
            GetSingleSelection(obj) is ISystemItem item && item.Location.IsLocal;
        public bool CanExecute_CopyPathCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable &&
            FileExplorerSelectionPolicy.NormalizeForOperation(obj).Count > 0;
        public bool CanExecute_CutCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable && CanExecuteMultiItemOperation(
                obj,
                StorageProviderCapabilities.Read | StorageProviderCapabilities.Delete);
        public bool CanExecute_CopyCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable && CanExecuteMultiItemOperation(
                obj,
                StorageProviderCapabilities.Read);
        public bool CanExecute_PasteCommand(object? obj)
        {
            if(!IsCurrentDirectoryUnavailable &&
               !string.IsNullOrEmpty(CurrentPath) &&
               SelectedItem is ISystemItem target &&
               Supports(target, StorageProviderCapabilities.Write))
            {
                return _fileClipboardService.GetData().SourcePaths.Count > 0;
            }
            return false;
        }
        public bool CanExecute_DeleteCommand(object? obj) =>
            !IsCurrentDirectoryUnavailable && CanExecuteMultiItemOperation(
                obj,
                StorageProviderCapabilities.Delete);
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
                _keyProvider.HasKey && CanExecuteMultiItemOperation(
                    obj,
                    StorageProviderCapabilities.Encrypt);
        }
        public bool CanExecute_EncryptCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
                _keyProvider.HasKey && CanExecuteMultiItemOperation(
                    obj,
                    StorageProviderCapabilities.Encrypt);
        }
        public bool CanExecute_CreateFileCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
                SelectedItem is IContainerSystemItem container &&
                Supports(container, StorageProviderCapabilities.Write);
        }
        public bool CanExecute_CreateDirectoryCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
                SelectedItem is IContainerSystemItem container &&
                Supports(container, StorageProviderCapabilities.CreateContainer);
        }
        public bool CanExecute_RenameClickCommand(object? obj)
        {
            return !IsCurrentDirectoryUnavailable &&
                   GetSingleSelection(obj) is ISystemItem item &&
                   item is not IDriveItem &&
                   Supports(item, StorageProviderCapabilities.Rename) &&
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
                       .All(item => Supports(
                           item,
                           StorageProviderCapabilities.Read |
                           StorageProviderCapabilities.Delete)) &&
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
                    StorageLocation source = _storage.Resolve(sourcePath);
                    StorageLocation destination = _storage.Resolve(
                        request.DestinationDirectory);
                    if(_storage.AreEquivalent(source, destination) ||
                       _storage.IsDescendant(source, destination))
                        return false;
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
            StorageLocation? parent = string.IsNullOrWhiteSpace(CurrentPath)
                ? null
                : _storage.GetParent(_storage.Resolve(CurrentPath));
            if(parent is not null)
            {
                await NavigateAsync(
                    _storage.Format(parent.Value),
                    FileExplorerNavigationMode.Standard);
            }
        }

        public async void Execute_ApplyAddressCommand(object? obj)
        {
            await NavigateAsync(
                ResolveAddressPath(AddressText),
                FileExplorerNavigationMode.Standard);
        }

        public void Execute_CancelAddressCommand(object? obj)
        {
            AddressText = CurrentDisplayPath;
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
            StorageLocation? directory = string.IsNullOrWhiteSpace(filePath)
                ? null
                : _storage.GetParent(_storage.Resolve(filePath));
            if(filePath is null || directory is null)
                return;

            bool navigated = await NavigateAsync(
                _storage.Format(directory.Value),
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
                        operation,
                        synchronizeViewAsync: () => RefreshOperationContainersAsync(
                            clipboard.SourcePaths,
                            CurrentPath,
                            CancellationToken.None));
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

            if(items.Any(item => !item.Location.IsLocal))
            {
                Guid confirmationId = await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.PermanentDeleteTitle"),
                    LocalizationManager.GetString("Explorer.PermanentDeleteAndroidWarning"),
                    true);
                if(!_messageService.ShowConfirmation(confirmationId))
                    return;
            }

            try
            {
                FileOperationBatchResult result = await _fileOperationCoordinator.DeleteAsync(
                    items.Select(item => item.FullPath),
                    synchronizeViewAsync: () => RefreshOperationContainersAsync(
                        items.Select(item => item.FullPath),
                        null,
                        CancellationToken.None));
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

            StorageLocation directoryLocation = _storage.GetChild(
                _storage.Resolve(container.FullPath),
                directoryName);
            string directoryPath = _storage.Format(directoryLocation);
            if(await _storage.GetMetadataAsync(directoryLocation) is not null)
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
            StorageItemMetadata metadata = await _storage.GetMetadataAsync(
                directoryLocation) ?? new StorageItemMetadata(
                    directoryLocation,
                    directoryName,
                    StorageItemKind.Container,
                    Capabilities: _storage.GetCapabilities(directoryLocation));
            var directoryItem = _systemItemCreateService.CreateDirectory(metadata, container);
            await container.AddChildAsync( [directoryItem], item => item.FullPath, CancellationToken.None);
            await container.SortingAsync( SystemItemSortType.Name, 0, CancellationToken.None);
        }
        public async void Execute_RenameClickCommand(object? obj)
        {
            if(GetSingleSelection(obj) is ISystemItem systemItem &&
               systemItem is not IDriveItem)
            {
                if(!IsFlatViewEnabled &&
                   systemItem.Parent is IContainerSystemItem parent &&
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
                if(string.IsNullOrWhiteSpace(systemItem.Name))
                {
                    systemItem.Name = _lastItemName;
                    return;
                }
                //выполняем переименование
                string oldPath = systemItem.FullPath;
                StorageLocation? directory = _storage.GetParent(
                    _storage.Resolve(oldPath));
                string? newPath = directory is null
                    ? null
                    : _storage.Format(_storage.GetChild(
                        directory.Value,
                        systemItem.Name));
                var res = await _fileManagerService.RenameAsync(oldPath, systemItem.Name, CancellationToken.None);
                if(res.Success)
                {
                    if(!string.IsNullOrWhiteSpace(newPath))
                        systemItem.FullPath = newPath;

                    if(res.Success && !string.IsNullOrWhiteSpace(newPath))
                        await SynchronizeRenamedDocumentAsync(oldPath, newPath);
                }
                if(!res.Success)
                {
                    _ = await _messageService.ShowMessage(
                        LocalizationManager.GetString(
                            "Explorer.RenameError"),
                        res.ErrorMessage);
                    systemItem.Name = _lastItemName;
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
                StorageLocation leftLocation = StorageLocation.Parse(left);
                StorageLocation rightLocation = StorageLocation.Parse(right);
                return leftLocation.ProviderId.Equals(
                        rightLocation.ProviderId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        leftLocation.OpaqueId.TrimEnd('\\', '/'),
                        rightLocation.OpaqueId.TrimEnd('\\', '/'),
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
                    items[0].Parent?.FullPath ?? _storage.GetParent(
                        items[0].Location)?.ToString(),
                    CancellationToken.None);
                if(string.IsNullOrWhiteSpace(destinationDirectory))
                    return;

                FileOperationBatchResult result = await _fileOperationCoordinator.TransferAsync(
                    items.Select(item => item.FullPath),
                    destinationDirectory,
                    FileTransferKind.Move,
                    synchronizeViewAsync: () => RefreshOperationContainersAsync(
                        items.Select(item => item.FullPath),
                        destinationDirectory,
                        CancellationToken.None));
                if(result.Failure is not null)
                {
                    await _messageService.ShowMessage(
                        LocalizationManager.GetString("Explorer.MoveError"),
                        result.Failure.ErrorMessage);
                }

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

            // List/address navigation selects the matching tree node after
            // CurrentPath has already changed. SelectedItemChanged must not
            // start the same navigation and collection refresh a second time.
            if(IsRedundantTreeSelection(
                container,
                SelectedItem,
                CurrentPath,
                IsCurrentDirectoryUnavailable))
            {
                return;
            }

            // Выбор узла не меняет IsExpanded: раскрытием управляет сам TreeView.
            await NavigateAsync(
                container.FullPath,
                FileExplorerNavigationMode.Standard);
        }

        internal static bool IsRedundantTreeSelection(
            IContainerSystemItem candidate,
            ISystemItem? selectedItem,
            string currentPath,
            bool isCurrentDirectoryUnavailable) =>
            !isCurrentDirectoryUnavailable &&
            ReferenceEquals(selectedItem, candidate) &&
            PathsEqual(candidate.FullPath, currentPath);

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

            var lockTaken = false;
            try
            {
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _cancellationTokenSource.Token);
                CancellationToken token = linkedCancellation.Token;

                await _gate.WaitAsync(token);
                lockTaken = true;

                StorageLocation requestedLocation = _storage.Resolve(path);
                StorageItemMetadata? targetMetadata = await _storage.GetMetadataAsync(
                    requestedLocation,
                    token);
                string targetPath = _storage.Format(
                    targetMetadata?.Location ?? requestedLocation);
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
                AddressText = GetDisplayPath(targetPath);
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
                    path,
                    FileExplorerNavigationErrorClassifier.Classify(
                        ex,
                        path));
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
            StorageLocation target = _storage.Resolve(path);
            var ancestry = new Stack<StorageLocation>();
            StorageLocation rootLocation = target;
            StorageLocation? parent;
            while((parent = _storage.GetParent(rootLocation)) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ancestry.Push(rootLocation);
                rootLocation = parent.Value;
            }

            IContainerSystemItem root = GetDrives
                .OfType<IContainerSystemItem>()
                .FirstOrDefault(item => _storage.AreEquivalent(
                    _storage.Resolve(item.FullPath),
                    rootLocation))
                ?? throw new DirectoryNotFoundException(
                    LocalizationManager.Format(
                        "Explorer.RootPathNotFound",
                        GetDisplayPath(path)));

            IContainerSystemItem current = root;
            while(ancestry.Count > 0)
            {
                StorageLocation childLocation = ancestry.Pop();
                FileOperationResult loadResult = await ContainerLoad(
                    current,
                    cancellationToken);
                if(!loadResult.Success)
                    throw new IOException(loadResult.ErrorMessage);

                current.IsExpanded = true;
                IContainerSystemItem? existing = current.Children
                    .OfType<IContainerSystemItem>()
                    .FirstOrDefault(item => _storage.AreEquivalent(
                        _storage.Resolve(item.FullPath),
                        childLocation));

                if(existing is null)
                {
                    throw new DirectoryNotFoundException(
                        _storage.FormatDisplayPath(childLocation));
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
                    GetDisplayPath(path),
                    Environment.NewLine,
                    error));

        private string GetDisplayPath(string path)
        {
            if(string.IsNullOrWhiteSpace(path))
                return path;

            try
            {
                return _storage.FormatDisplayPath(_storage.Resolve(path));
            }
            catch(ArgumentException)
            {
                return path;
            }
            catch(NotSupportedException)
            {
                return path;
            }
            catch(FormatException)
            {
                return path;
            }
        }

        private string ResolveAddressPath(string address)
        {
            if(string.IsNullOrWhiteSpace(address) ||
               string.IsNullOrWhiteSpace(CurrentPath) ||
               address.Contains("://", StringComparison.Ordinal))
            {
                return address;
            }

            StorageLocation current = _storage.Resolve(CurrentPath);
            if(current.IsLocal || LooksLikeLocalPath(address))
                return address;

            StorageLocation location = _storage.ResolveDisplayPath(
                current,
                address);
            return _storage.Format(location);
        }

        private static bool LooksLikeLocalPath(string path) =>
            Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal);

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
                    case IContainerSystemItem container when Supports(
                        container,
                        StorageProviderCapabilities.Browse):
                        await NavigateAsync(
                            container.FullPath,
                            FileExplorerNavigationMode.Standard);
                        break;
                    case IFileItem file when !file.IsEditing && Supports(
                        file,
                        StorageProviderCapabilities.OpenExternally):
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
            return children.All(item => container.Children.Any(existing =>
                _storage.AreEquivalent(
                    _storage.Resolve(existing.FullPath),
                    _storage.Resolve(item.FullPath))))
                    ? FileOperationResult.Ok()
                    : result;
        }

        private bool TryValidateDirectoryName(string name, out string error)
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
               !_localFiles.IsValidFileName(name))
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
                InitialDirectory = _localFiles.GetParent(sourcePath),
                FileName = _localFiles.GetNameWithoutExtension(sourcePath) +
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

                foreach(ISystemItem systemItem in items)
                {
                    if(systemItem is not IFileItem && systemItem is not IDirectoryItem)
                        return;

                    string sourcePath = systemItem.FullPath;
                    if(string.IsNullOrWhiteSpace(sourcePath) || !_localFiles.Exists(sourcePath))
                    {
                        await _messageService.ShowMessage(
                            errorTitle,
                            LocalizationManager.GetString(
                                "Explorer.ItemDoesNotExist"));
                        return;
                    }
                }

                if(items.Count > 1)
                {
                    if(decrypt)
                    {
                        await ExecuteBatchDecryptionAsync(items, errorTitle);
                        return;
                    }

                    // Пакетная операция заменяет элементы на месте: единый Save As
                    // не может однозначно задать назначения для разных источников.
                    if(!await ConfirmBatchSourceReplacementAsync(items.Count, decrypt: false))
                        return;

                    FileOperationBatchResult batchResult = await _progressDialogService.RunAsync(
                        LocalizationManager.GetString(
                            decrypt
                                ? "Explorer.Decryption"
                                : "Explorer.Encryption"),
                        async (progress, token) =>
                        {
                            FileOperationBatchResult operationResult =
                                await _fileSecurityService.EncryptAsync(
                                    items,
                                    progress,
                                    token);

                            progress.Report(
                                null,
                                LocalizationManager.GetString(
                                    "Explorer.RefreshingAfterOperation"));
                            await RefreshFileSecurityItemsAsync(
                                items.Take(operationResult.Results.Count));
                            return operationResult;
                        });

                    if(batchResult.Canceled)
                        return;

                    if(!batchResult.Success)
                    {
                        string failures = string.Join(
                            Environment.NewLine + Environment.NewLine,
                            batchResult.Results
                                .Where(result => !result.Success)
                                .Select(result => result.ErrorMessage));
                        await _messageService.ShowMessage(
                            errorTitle,
                            LocalizationManager.Format(
                                "Explorer.BatchOperationFailed",
                                Environment.NewLine,
                                failures));
                        return;
                    }

                    return;
                }

                ISystemItem item = items[0];
                string itemPath = item.FullPath;
                if(decrypt && item is IFileItem &&
                   IsCryptoBookContainerPath(itemPath))
                {
                    await ExecuteSingleFileDecryptionAsync(
                        item,
                        itemPath);
                    return;
                }

                (EncryptionTargetMode mode, string? targetPath) =
                    ResolveFileSecurityTarget(item, itemPath, decrypt);

                if(mode == EncryptionTargetMode.Cancels ||
                   string.IsNullOrWhiteSpace(targetPath))
                {
                    return;
                }

                if(decrypt && mode == EncryptionTargetMode.SaveAs)
                {
                    await ExecuteDecryptionCopyAsync(item, itemPath, errorTitle);
                    return;
                }

                if(mode == EncryptionTargetMode.ReplaceSource &&
                   !await ConfirmSourceReplacementAsync(item, itemPath, decrypt))
                {
                    return;
                }

                FileOperationResult result = await _progressDialogService.RunAsync(
                    LocalizationManager.GetString(
                        decrypt
                            ? "Explorer.Decryption"
                            : "Explorer.Encryption"),
                    async (progress, token) =>
                    {
                        FileOperationResult operationResult = decrypt
                            ? await _fileSecurityService.DecryptAsync(
                                item,
                                targetPath,
                                mode,
                                progress,
                                token)
                            : await _fileSecurityService.EncryptAsync(
                                item,
                                targetPath,
                                mode,
                                progress,
                                token);

                        progress.Report(
                            null,
                            LocalizationManager.GetString(
                                "Explorer.RefreshingAfterOperation"));
                        await RefreshFileSecurityItemsAsync(
                            [item],
                            targetPath);
                        return operationResult;
                    });

                if(!result.Success)
                {
                    await _messageService.ShowMessage(errorTitle, result.ErrorMessage);
                    return;
                }

            } catch(OperationCanceledException)
            {
                // Отмена пользователем является штатным завершением операции.
            } catch(Exception ex)
            {
                await _messageService.ShowMessage(errorTitle, ex.Message);
            }
        }

        private async Task ExecuteDecryptionCopyAsync(
            ISystemItem item,
            string sourcePath,
            string errorTitle)
        {
            if(item is IDirectoryItem)
            {
                string? destinationDirectory = await _folderPickerService
                    .PickFolderAsync(
                        _localFiles.GetParent(sourcePath),
                        CancellationToken.None);
                if(string.IsNullOrWhiteSpace(destinationDirectory))
                    return;

                FileOperationBatchResult directoryResult =
                    await _progressDialogService.RunAsync(
                        LocalizationManager.GetString(
                            "Explorer.Decryption"),
                        (progress, token) =>
                            _fileSecurityService.DecryptAsync(
                                [item],
                                destinationDirectory,
                                progress,
                                token));
                if(!directoryResult.Success)
                {
                    string failures = string.Join(
                        Environment.NewLine + Environment.NewLine,
                        directoryResult.Results
                            .Where(result => !result.Success)
                            .Select(result => result.ErrorMessage));
                    await _messageService.ShowMessage(
                        errorTitle,
                        LocalizationManager.Format(
                            "Explorer.BatchOperationFailed",
                            Environment.NewLine,
                            failures));
                }

                await RefreshFileSecurityItemsAsync(
                    [item],
                    destinationDirectory);
                await ShowDecryptionSummaryAsync(directoryResult);
                return;
            }

            string temporaryDirectory = _localFiles.Combine(
                GetDecryptionTemporaryRoot(),
                $"{Environment.ProcessId}-{Guid.NewGuid():N}");
            _localFiles.EnsureDirectory(temporaryDirectory);

            try
            {
                FileOperationResult result =
                    await _progressDialogService.RunAsync(
                        LocalizationManager.GetString(
                            "Explorer.Decryption"),
                        (progress, token) =>
                            _fileSecurityService.DecryptAsync(
                                item,
                                _localFiles.Combine(
                                    temporaryDirectory,
                                    "payload"),
                                EncryptionTargetMode.SaveAs,
                                progress,
                                token));

                if(!result.Success ||
                   string.IsNullOrWhiteSpace(result.AffectedPath) ||
                   !_localFiles.FileExists(result.AffectedPath))
                {
                    await _messageService.ShowMessage(
                        errorTitle,
                        result.ErrorMessage ??
                            LocalizationManager.GetString(
                                "Security.DecryptionResultUnknown"));
                    return;
                }

                string? destinationPath = GetNewDecryptedFilePath(
                    sourcePath,
                    _localFiles.GetExtension(result.AffectedPath));
                if(string.IsNullOrWhiteSpace(destinationPath))
                    return;

                await _progressDialogService.RunAsync(
                    LocalizationManager.GetString(
                        "Explorer.SavingDecryptedCopy"),
                    async (progress, token) =>
                    {
                        await PublishDecryptedCopyAsync(
                            result.AffectedPath,
                            destinationPath,
                            progress,
                            token);
                        return true;
                    });

                await RefreshFileSecurityItemsAsync(
                    [item],
                    destinationPath);
            }
            finally
            {
                TryDeleteDecryptionDirectory(temporaryDirectory);
            }
        }

        private async Task ExecuteSingleFileDecryptionAsync(
            ISystemItem item,
            string sourcePath)
        {
            await using PreparedDecryption prepared =
                await _progressDialogService.RunAsync(
                    LocalizationManager.GetString("Explorer.Decryption"),
                    (progress, token) => _decryptionExportService.PrepareAsync(
                        sourcePath,
                        progress,
                        token));

            IReadOnlyList<DecryptionOutputFormat> availableFormats =
                _decryptionExportService.GetAvailableFormats(
                    prepared.OriginalExtension);
            var context = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>
                {
                    ["sourcePath"] = sourcePath,
                    ["originalExtension"] = prepared.OriginalExtension,
                    ["availableFormats"] = availableFormats,
                    ["defaultFormat"] = _decryptionExportService
                        .GetDefaultFormat(prepared.OriginalExtension)
                });
            Guid windowId = _windowManager.CreateWindow<
                DecryptionOptionsWindow>(context);
            _windowManager.ShowWindowDialog(windowId);
            DecryptionOptions? options =
                _windowManager.GetResult<DecryptionOptions>(windowId);
            if(options is null)
                return;

            if(options.TargetMode == EncryptionTargetMode.ReplaceSource)
            {
                if(!await ConfirmSourceReplacementAsync(
                    item,
                    sourcePath,
                    decrypt: true))
                {
                    return;
                }

                if(options.OutputFormat ==
                       DecryptionOutputFormat.PlainText &&
                   !await ConfirmPlainTextSourceReplacementAsync(sourcePath))
                {
                    return;
                }
            }

            string extension = _decryptionExportService.GetOutputExtension(
                prepared.OriginalExtension,
                options.OutputFormat);
            string? destinationPath = options.TargetMode switch
            {
                EncryptionTargetMode.SaveAs =>
                    GetNewDecryptedFilePath(sourcePath, extension),
                EncryptionTargetMode.ReplaceSource => _localFiles.Combine(
                    _localFiles.GetParent(sourcePath) ??
                        throw new IOException(
                            LocalizationManager.Format(
                                "Security.SourceDirectoryUnknown",
                                sourcePath)),
                    _localFiles.GetNameWithoutExtension(sourcePath) + extension),
                _ => null
            };
            if(string.IsNullOrWhiteSpace(destinationPath))
                return;

            string publishedPath = await _progressDialogService.RunAsync(
                LocalizationManager.GetString(
                    "Explorer.SavingDecryptedCopy"),
                (progress, token) => _decryptionExportService.PublishAsync(
                    prepared,
                    options,
                    destinationPath,
                    progress,
                    token));

            await RefreshFileSecurityItemsAsync([item], publishedPath);
        }

        private async Task<bool> ConfirmPlainTextSourceReplacementAsync(
            string sourcePath)
        {
            Guid messageId = await _messageService.ShowMessage(
                LocalizationManager.GetString(
                    "DecryptionOptions.PlainTextReplaceTitle"),
                LocalizationManager.Format(
                    "DecryptionOptions.PlainTextReplaceConfirmation",
                    Environment.NewLine,
                    sourcePath),
                true);
            return _messageService.ShowConfirmation(messageId);
        }

        private bool IsCryptoBookContainerPath(string path) =>
            _localFiles.GetExtension(path).Equals(
                ".cbook",
                StringComparison.OrdinalIgnoreCase) ||
            _localFiles.GetExtension(path).Equals(
                ".cbox",
                StringComparison.OrdinalIgnoreCase);

        private string GetDecryptionTemporaryRoot() =>
            _localFiles.TemporaryPath("CryptoBook", "Decrypt");

        private void CleanupOrphanedDecryptionDirectories(
            string temporaryRoot)
        {
            foreach(string directory in _localFiles.EnumerateDirectories(temporaryRoot))
            {
                string name = _localFiles.GetName(directory);
                int separator = name.IndexOf('-');
                if(separator <= 0 ||
                   !int.TryParse(
                       name.AsSpan(0, separator),
                       out int processId) ||
                   IsProcessRunning(processId))
                {
                    continue;
                }

                TryDeleteDecryptionDirectory(directory);
            }
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch(ArgumentException)
            {
                return false;
            }
        }

        private void TryDeleteDecryptionDirectory(string path) =>
            _localFiles.DeleteDirectoryIfExists(path);

        private Task PublishDecryptedCopyAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken) =>
            _localFiles.CopyFileAtomicallyAsync(
                sourcePath,
                destinationPath,
                progress,
                cancellationToken);

        private async Task ExecuteBatchDecryptionAsync(
            IReadOnlyList<ISystemItem> items,
            string errorTitle)
        {
            ISystemItem firstItem = items[0];
            (EncryptionTargetMode mode, _) = ResolveFileSecurityTarget(
                firstItem,
                firstItem.FullPath,
                decrypt: true);
            if(mode == EncryptionTargetMode.Cancels)
                return;

            string? destinationDirectory = null;
            if(mode == EncryptionTargetMode.ReplaceSource)
            {
                if(!await ConfirmBatchSourceReplacementAsync(
                    items.Count,
                    decrypt: true))
                {
                    return;
                }
            }
            else
            {
                destinationDirectory = await _folderPickerService
                    .PickFolderAsync(
                        _localFiles.GetParent(firstItem.FullPath),
                        CancellationToken.None);
                if(string.IsNullOrWhiteSpace(destinationDirectory))
                    return;
            }

            FileOperationBatchResult batchResult =
                await _progressDialogService.RunAsync(
                    LocalizationManager.GetString("Explorer.Decryption"),
                    async (progress, token) =>
                    {
                        FileOperationBatchResult operationResult =
                            destinationDirectory is null
                                ? await _fileSecurityService.DecryptAsync(
                                    items,
                                    progress,
                                    token)
                                : await _fileSecurityService.DecryptAsync(
                                    items,
                                    destinationDirectory,
                                    progress,
                                    token);

                        progress.Report(
                            null,
                            LocalizationManager.GetString(
                                "Explorer.RefreshingAfterOperation"));
                        await RefreshFileSecurityItemsAsync(
                            items.Take(operationResult.Results.Count),
                            destinationDirectory);
                        return operationResult;
                    });

            if(batchResult.Canceled)
                return;

            if(!batchResult.Success)
            {
                string failures = string.Join(
                    Environment.NewLine + Environment.NewLine,
                    batchResult.Results
                        .Where(result => !result.Success)
                        .Select(result => result.ErrorMessage));
                await _messageService.ShowMessage(
                    errorTitle,
                    LocalizationManager.Format(
                        "Explorer.BatchOperationFailed",
                        Environment.NewLine,
                        failures));
            }

            await ShowDecryptionSummaryAsync(batchResult);
        }

        private Task<Guid> ShowDecryptionSummaryAsync(
            FileOperationBatchResult result) =>
            _messageService.ShowMessage(
                LocalizationManager.GetString(
                    "Explorer.DecryptionResultTitle"),
                LocalizationManager.Format(
                    "Explorer.DecryptionBatchResult",
                    result.Results.Sum(item => item.ProcessedFileCount),
                    result.SkippedCount));

        private async Task<bool> ConfirmBatchSourceReplacementAsync(
            int itemCount,
            bool decrypt)
        {
            string replacement = LocalizationManager.GetString(
                decrypt
                    ? "Explorer.DecryptedCopiesAdjective"
                    : "Explorer.EncryptedCopiesAdjective");
            string warning = LocalizationManager.Format(
                "Explorer.OverwriteBatchPrompt",
                itemCount,
                replacement);
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

        private async Task RefreshFileSecurityItemsAsync(
            IEnumerable<ISystemItem> items,
            string? singleTargetPath = null)
        {
            try
            {
                var affectedPaths = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                foreach(ISystemItem item in items)
                {
                    // Синхронизация нужна, если FileSystemWatcher
                    // пропустил быструю замену. Один загруженный каталог
                    // обновляем один раз для всего пакета.
                    AddAffectedContainerPaths(
                        affectedPaths,
                        item.FullPath,
                        item is IDirectoryItem);
                    AddAffectedContainerPaths(
                        affectedPaths,
                        singleTargetPath ?? item.FullPath,
                        item is IDirectoryItem);
                }

                var containers = EnumerateLoadedContainers()
                    .Where(container => affectedPaths.Contains(
                        NormalizePath(container.FullPath)))
                    .Distinct()
                    .ToList();

                foreach(IContainerSystemItem container in containers)
                {
                    await RefreshContainerAsync(
                        container,
                        CancellationToken.None);
                }
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString(
                        "Explorer.RefreshError"),
                    LocalizationManager.Format(
                        "Explorer.RefreshAfterOperationFailed",
                        Environment.NewLine,
                        ex.Message));
            }
        }

        private (EncryptionTargetMode Mode, string? TargetPath) ResolveFileSecurityTarget( ISystemItem systemItem, string sourcePath, bool decrypt)
        {
            // Шифрование каталога по-прежнему выполняется только на месте.
            if(systemItem is IDirectoryItem && !decrypt)
                return (EncryptionTargetMode.ReplaceSource, sourcePath);

            var context = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>
                {
                    ["path"] = systemItem,
                    ["decrypt"] = decrypt
                });

            Guid windowId = _windowManager.CreateWindow<EncryptionModeWindow>(context);
            _windowManager.ShowWindowDialog(windowId);

            EncryptionTargetMode mode =
                _windowManager.GetResult<EncryptionTargetMode>(windowId);
            if(mode == EncryptionTargetMode.Cancels)
                return (mode, null);

            string? targetPath = mode switch
            {
                EncryptionTargetMode.ReplaceSource => sourcePath,
                EncryptionTargetMode.SaveAs when decrypt => sourcePath,
                EncryptionTargetMode.SaveAs => GetNewFilePath(sourcePath),
                _ => null
            };

            if(!decrypt && !string.IsNullOrWhiteSpace(targetPath))
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
            await WaitForFileExplorerRenderAsync(token);
        }

        private async Task WaitForFileExplorerRenderAsync(CancellationToken token)
        {
            Window? explorerWindow = _windowManager.FindHostWindow(WindowId)?.Window;
            if(explorerWindow is not { IsLoaded: true, IsVisible: true })
                return;

            await _viewRenderSynchronizationService.WaitForRenderAsync(
                explorerWindow,
                token);
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
                StorageLocation? parent = _storage.GetParent(
                    _storage.Resolve(sourcePath));
                if(parent is not null)
                    affectedPaths.Add(_storage.Format(parent.Value));
            }

            if(!string.IsNullOrWhiteSpace(destinationDirectory))
                affectedPaths.Add(_storage.Format(
                    _storage.Resolve(destinationDirectory)));

            foreach(IContainerSystemItem container in EnumerateLoadedContainers()
                .Where(container => affectedPaths.Any(path =>
                    _storage.AreEquivalent(
                        _storage.Resolve(path),
                        _storage.Resolve(container.FullPath))))
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
                    request.Operation,
                    synchronizeViewAsync: () => RefreshOperationContainersAsync(
                        request.SourcePaths,
                        request.DestinationDirectory,
                        CancellationToken.None));
                if(result.Failure is not null)
                {
                    await _messageService.ShowMessage(
                        LocalizationManager.GetString(
                            request.Operation == FileTransferKind.Copy
                                ? "Explorer.CopyError"
                                : "Explorer.MoveError"),
                        result.Failure.ErrorMessage);
                }

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

        private void AddAffectedContainerPaths(
            ISet<string> paths,
            string itemPath,
            bool isDirectory)
        {
            if(string.IsNullOrWhiteSpace(itemPath))
                return;

            if(isDirectory)
                paths.Add(NormalizePath(itemPath));

            StorageLocation? parent = _storage.GetParent(
                _storage.Resolve(itemPath));
            if(parent is not null)
                paths.Add(_storage.Format(parent.Value));
        }

        private string NormalizePath(string path) =>
            _storage.Format(_storage.Resolve(path));

        private static ISystemItem? GetSingleSelection(object? selection)
        {
            IReadOnlyList<ISystemItem> snapshot =
                FileExplorerSelectionPolicy.CreateSnapshot(selection);
            return snapshot.Count == 1 ? snapshot[0] : null;
        }

        private static bool CanExecuteMultiItemOperation(
            object? selection,
            StorageProviderCapabilities required = StorageProviderCapabilities.None)
        {
            if(FileExplorerSelectionPolicy.ContainsDrive(selection))
                return false;
            IReadOnlyList<ISystemItem> items =
                FileExplorerSelectionPolicy.NormalizeForOperation(selection);
            return items.Count > 0 && items.All(item => Supports(item, required));
        }

        private static bool Supports(
            ISystemItem item,
            StorageProviderCapabilities capability) =>
            capability == StorageProviderCapabilities.None ||
            item.Capabilities.HasFlag(capability) ||
            (item.Capabilities == StorageProviderCapabilities.None &&
             item.Location.IsLocal);

        private static IReadOnlyList<ISystemItem> GetOperationSelection(
            object? selection) =>
            FileExplorerSelectionPolicy.ContainsDrive(selection)
                ? Array.Empty<ISystemItem>()
                : FileExplorerSelectionPolicy.NormalizeForOperation(selection);

        private static void UpdateSystemItem(ISystemItem existing, ISystemItem incoming)
        {
            existing.Name = incoming.Name;
            existing.FullPath = incoming.FullPath;
            existing.DisplayPath = incoming.DisplayPath;
            existing.RootDirectory = incoming.RootDirectory;
            existing.Size = incoming.Size;
            existing.LastWriteTimeUtc = incoming.LastWriteTimeUtc;
            existing.Parent = incoming.Parent;
            existing.Capabilities = incoming.Capabilities;
            existing.StatusText = incoming.StatusText;

            if(existing is IFileItem existingFile && incoming is IFileItem incomingFile)
            {
                existingFile.Extension = incomingFile.Extension;
                existingFile.IsHidden = incomingFile.IsHidden;
                existingFile.IsReadOnly = incomingFile.IsReadOnly;
            }
        }

        private string? GetNewDecryptedFilePath(
            string sourcePath,
            string extension)
        {
            extension = string.IsNullOrWhiteSpace(extension)
                ? string.Empty
                : extension.StartsWith('.') ? extension : $".{extension}";
            string extensionPattern = string.IsNullOrWhiteSpace(extension)
                ? "*.*"
                : $"*{extension}";
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = _localFiles.GetParent(sourcePath),
                FileName = _localFiles.GetNameWithoutExtension(sourcePath) +
                    "_" + LocalizationManager.GetString(
                        "Explorer.DecryptedSuffix") + extension,
                DefaultExt = extension,
                AddExtension = !string.IsNullOrWhiteSpace(extension),
                Filter = $"{extensionPattern}|{extensionPattern}|" +
                    LocalizationManager.GetString(
                        "Explorer.AllFilesFilter"),
                FilterIndex = 1,
                OverwritePrompt = true,
                CheckPathExists = true,
                ValidateNames = true,
                Title = LocalizationManager.GetString(
                    "Explorer.SaveDecryptedTitle")
            };

            return dialog.ShowDialog() == true
                ? _localFiles.ChangeExtension(dialog.FileName, extension)
                : null;
        }

    }
}
