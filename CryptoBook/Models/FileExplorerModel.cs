using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Properties;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.Views;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;

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
        private readonly IWorkspaceFileOpenService _fileOpenService;
        private readonly IDocumentSession _documentSession;
        private readonly IPinnedDocumentService _pinnedDocumentService;

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
        public string CurrentPath { get => _currentPath; set => SetProperty(ref _currentPath, value); }
        private string _currentPath;
        public ReadOnlyObservableCollection<IDriveItem> GetDrives { get; private set; }
        private string _lastItemName;


        public FileExplorerModel(IFileManagerService? fileManagerService, IDriveManagerService? driveManagerService,
            IWindowManager? windowManager, IFileClipboardService fileClipboardService, IFolderPickerService folderPickerService, IMessageService messageService, IKeyProvider keyProvider, IFileSecurityService fileSecurityService, ISystemItemCreateService systemItemCreateService, IProgressDialogService progressDialogService, IWorkspaceFileOpenService fileOpenService, IDocumentSession documentSession, IPinnedDocumentService pinnedDocumentService)
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
            _fileOpenService = fileOpenService ?? throw new ArgumentNullException(nameof(fileOpenService));
            _documentSession = documentSession ?? throw new ArgumentNullException(nameof(documentSession));
            _pinnedDocumentService = pinnedDocumentService ?? throw new ArgumentNullException(nameof(pinnedDocumentService));
            GetDrives = _driveManagerService.WritableDrives;
        }


        public bool CanExecute_BackCommand(object? obj) => SelectedItem?.Parent is not null;
        public bool CanExecute_CutCommand(object? obj) => obj is IList { Count: > 0 };
        public bool CanExecute_CopyCommand(object? obj) => obj is IList { Count: > 0 };
        public bool CanExecute_PasteCommand(object? obj)
        {
            if(!string.IsNullOrEmpty(CurrentPath))
            {
                return _fileClipboardService.GetData().SourcePaths.Count > 0;
            }
            return false;
        }
        public bool CanExecute_DeleteCommand(object? obj) => obj is ISystemItem;
        public bool CanExecute_SortedCommand(object? obj)
        {
            return obj is string name && !string.IsNullOrWhiteSpace(name) &&
            SelectedItem is IContainerSystemItem item && item.Children.Count > 1;
        }
        public bool CanExecute_EncryptingKeyCommand(object? obj)
        {
            return true;
        }
        public bool CanExecute_DecryptCommand(object? obj)
        {
            return _keyProvider.HasKey && obj is ISystemItem systemItem;
        }
        public bool CanExecute_EncryptCommand(object? obj)
        {
            return _keyProvider.HasKey && obj is ISystemItem systemItem;
        }
        public bool CanExecute_CreateFileCommand(object? obj)
        {
            return SelectedItem is IContainerSystemItem;
        }
        public bool CanExecute_CreateDirectoryCommand(object? obj)
        {
            return SelectedItem is IContainerSystemItem;
        }
        public bool CanExecute_RenameClickCommand(object? obj)
        {
            return obj is ISystemItem item && !item.IsEditing;
        }
        public bool CanExecute_RenameCommand(object? obj)
        {
            return obj is not null;
        }
        public bool CanExecute_MoveCommand(object? obj)
        {
            return obj is ISystemItem item &&
                   item.Parent is not null &&
                   !string.IsNullOrWhiteSpace(item.FullPath);
        }
        public bool CanExecure_RefreshCommand(object? obj)
        {
            return obj is IContainerSystemItem;
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
            return obj is not null;
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
            _cancellationTokenSource = new CancellationTokenSource();
            await _gate.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                if(SelectedItem is IContainerSystemItem currentItem && currentItem.Parent is IContainerSystemItem parentItem)
                {

                    SelectedItem = parentItem;
                    CurrentPath = parentItem.FullPath;
                    var res = await ContainerLoad(parentItem, _cancellationTokenSource.Token);
                    parentItem.IsLoaded = res.Success;
                    parentItem.IsExpanded = true;
                    //currentItem.IsExpanded = false;
                }
            } catch
            {

                _cancellationTokenSource?.Cancel();
            } finally
            {
                _gate.Release();
            }
        }
        public void Execute_CutCommand(object? obj)
        {
            if(obj is IList list && list.Count > 0)
            {
                var systemItems = list.OfType<ISystemItem>().Where(si => si.FullPath is not null).Select(si => si.FullPath!).ToList();
                _fileClipboardService.SetMove(systemItems);
            } else
            {
                throw new ArgumentException("Invalid argument for CutCommand", nameof(obj));
            }
        }
        public void Execute_CopyCommand(object? obj)
        {
            if(obj is IList list && list.Count > 0)
            {
                var systemItems = list.OfType<ISystemItem>().Where(si => si.FullPath is not null).Select(si => si.FullPath!).ToList();
                _fileClipboardService.SetCopy(systemItems);
            } else
            {
                throw new ArgumentException("Invalid argument for CopyCommand", nameof(obj));
            }
        }
        public async void Execute_PasteCommand(object? obj)
        {
            if(!string.IsNullOrEmpty(CurrentPath) && _fileClipboardService.GetData().SourcePaths.Count > 0)
            {
                try
                {
                    IReadOnlyList<FileOperationResult> results =
                        await _progressDialogService.RunAsync(
                            LocalizationManager.GetString(
                                "Explorer.Copying"),
                            (progress, token) =>
                                _fileClipboardService.PasteAsync(CurrentPath, progress, token));

                    FileOperationResult? failure = results.FirstOrDefault(result => !result.Success);
                    if(failure is not null)
                    {
                        _ = await _messageService.ShowMessage(
                            LocalizationManager.GetString(
                                "Explorer.CopyError"),
                            failure.ErrorMessage);
                        return;
                    }

                    Execute_SortedCommand("Name");
                } catch(OperationCanceledException)
                {
                    // Отмена пользователем не является ошибкой копирования.
                } catch(Exception ex)
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
        public void Execute_DeleteCommand(object? obj)
        {
            if(obj is ISystemItem systemItem)
            {
                if(systemItem is IContainerSystemItem container)
                {
                    _ = Task.Run(async () =>
                    {
                        await _fileManagerService.DeleteAsync(container.FullPath, CancellationToken.None);
                    });
                } else if(systemItem is IFileItem file)
                {
                    _ = Task.Run(async () =>
                    {
                        await _fileManagerService.DeleteAsync(file.FullPath, CancellationToken.None);
                    });
                } else
                    throw new ArgumentException("Invalid ISystemItem type for DeleteCommand", nameof(obj));
            } else
            {
                throw new ArgumentException("Invalid argument for DeleteCommand", nameof(obj));
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
        public void Execute_RenameClickCommand(object? obj)
        {
            if(obj is ISystemItem systemItem)
            {
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
            }
            catch(Exception exception)
            {
                Debug.WriteLine(exception);
                _ = await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.RenameError"),
                    LocalizationManager.GetString(
                        "PinnedDocuments.RenameSyncFailed"));
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
            if(obj is not ISystemItem item || !CanExecute_MoveCommand(item))
                return;

            try
            {
                string? destinationDirectory = await _folderPickerService.PickFolderAsync(
                    item.Parent?.FullPath ?? Path.GetDirectoryName(item.FullPath),
                    CancellationToken.None);
                if(string.IsNullOrWhiteSpace(destinationDirectory))
                    return;

                string destinationPath = CombineManagerPath(
                    destinationDirectory,
                    item.Name);

                FileOperationResult result = await _progressDialogService.RunAsync(
                    LocalizationManager.GetString("Explorer.Moving"),
                    (progress, token) => _fileManagerService.MoveAsync(
                        item.FullPath,
                        destinationPath,
                        progress,
                        token));

                if(!result.Success)
                {
                    await _messageService.ShowMessage(
                        LocalizationManager.GetString("Explorer.MoveError"),
                        result.ErrorMessage);
                    return;
                }

                if(item.Parent is IContainerSystemItem sourceContainer)
                    await RefreshContainerAsync(sourceContainer, CancellationToken.None);

                string destinationNativePath = GetNativePath(destinationDirectory);
                IContainerSystemItem? destinationContainer = EnumerateLoadedContainers()
                    .FirstOrDefault(container =>
                        string.Equals(
                            NormalizePath(container.FullPath),
                            NormalizePath(destinationNativePath),
                            StringComparison.OrdinalIgnoreCase));
                if(destinationContainer is not null &&
                   !ReferenceEquals(destinationContainer, item.Parent))
                {
                    await RefreshContainerAsync(destinationContainer, CancellationToken.None);
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
            if(obj is not IContainerSystemItem container)
                return;

            try
            {
                await RefreshContainerAsync(container, CancellationToken.None);
            } catch(OperationCanceledException)
            {
            } catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString("Explorer.RefreshError"),
                    ex.Message);
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
            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
            var lockTaken = false;

            try
            {
                await _gate.WaitAsync(cancellationTokenSource.Token);
                lockTaken = true;

                SelectedItem = container;
                CurrentPath = container.FullPath;

                var result = await ContainerLoad(container, cancellationTokenSource.Token);
                container.IsLoaded = result.Success;
            } catch(OperationCanceledException)
            {
                // Отмена выбора не является ошибкой для UI.
            } finally
            {
                if(lockTaken)
                    _gate.Release();
            }

        }
        public async void Execute_ListViewItemDoubleClickCommand(object? obj)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
            var lockTaken = false;

            try
            {
                await _gate.WaitAsync(cancellationTokenSource.Token);
                lockTaken = true;

                switch(obj)
                {
                    case IContainerSystemItem container:
                        await OpenContainerAsync(container, cancellationTokenSource.Token);
                        break;
                    case IFileItem file when !file.IsEditing:
                        await OpenFileAsync(file, cancellationTokenSource.Token);
                        break;
                }
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                var itemName = obj is ISystemItem item
                    ? item.Name
                    : LocalizationManager.GetString("Common.File");
                _ = await _messageService.ShowMessage(
                    LocalizationManager.GetString(
                        "Explorer.FileOpenError"),
                    LocalizationManager.Format(
                        "Explorer.FileOpenFailed",
                        itemName,
                        Environment.NewLine,
                        ex.Message));
            }
            finally
            {
                if(lockTaken)
                    _gate.Release();
            }
        }

        private async Task OpenContainerAsync(
            IContainerSystemItem container,
            CancellationToken cancellationToken)
        {
            SelectedItem = container;
            CurrentPath = container.FullPath;

            var result = await ContainerLoad(container, cancellationToken);
            container.IsExpanded = true;
            container.IsLoaded = result.Success;
        }

        public async Task OpenDirectoryAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    LocalizationManager.GetString(
                        "Explorer.DirectoryPathRequired"),
                    nameof(path));

            string nativePath = GetNativePath(path);
            var lockTaken = false;
            try
            {
                await _gate.WaitAsync(cancellationToken);
                lockTaken = true;

                string targetPath = NormalizePath(nativePath);
                IContainerSystemItem? container = EnumerateAllContainers()
                    .FirstOrDefault(item =>
                        string.Equals(
                            NormalizePath(item.FullPath),
                            targetPath,
                            StringComparison.OrdinalIgnoreCase));
                container ??= ResolveDirectoryContainer(nativePath);
                await OpenContainerAsync(container, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                await _messageService.ShowMessage(
                    LocalizationManager.GetString(
                        "Explorer.OpenDirectoryError"),
                    LocalizationManager.Format(
                        "Explorer.OpenDirectoryFailed",
                        nativePath,
                        Environment.NewLine,
                        ex.Message));
            }
            finally
            {
                if(lockTaken)
                    _gate.Release();
            }
        }

        private IContainerSystemItem ResolveDirectoryContainer(string path)
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
                ?? _systemItemCreateService.CreateRoot(rootPath);

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
                currentPath = Path.Combine(currentPath, segment);
                IContainerSystemItem? existing = current.Children
                    .OfType<IContainerSystemItem>()
                    .FirstOrDefault(item =>
                        string.Equals(
                            NormalizePath(item.FullPath),
                            NormalizePath(currentPath),
                            StringComparison.OrdinalIgnoreCase));

                current = existing
                    ?? _systemItemCreateService.CreateDirectory(currentPath, current);
            }

            return current;
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

            return await container.AddChildAsync(children, x => x.FullPath, token);
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
                if(obj is IDriveItem)
                {
                    await _messageService.ShowMessage(
                        errorTitle,
                        LocalizationManager.GetString(
                            "Explorer.WholeDriveNotAllowed"));
                    return;
                }

                if(obj is not IFileItem && obj is not IDirectoryItem)
                    return;

                var systemItem = (ISystemItem)obj;
                string sourcePath = systemItem.FullPath;
                if(string.IsNullOrWhiteSpace(sourcePath) || !Path.Exists(sourcePath))
                {
                    await _messageService.ShowMessage(
                        errorTitle,
                        LocalizationManager.GetString(
                            "Explorer.ItemDoesNotExist"));
                    return;
                }

                (EncryptionTargetMode mode, string? targetPath) =
                    ResolveFileSecurityTarget(systemItem, sourcePath, decrypt);

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

                FileOperationResult result = await _progressDialogService.RunAsync(
                    LocalizationManager.GetString(
                        decrypt
                            ? "Explorer.Decryption"
                            : "Explorer.Encryption"),
                    (progress, token) => decrypt
                        ? _fileSecurityService.DecryptAsync(systemItem, targetPath, mode, progress, token)
                        : _fileSecurityService.EncryptAsync(systemItem, targetPath, mode, progress, token));

                if(!result.Success)
                {
                    await _messageService.ShowMessage(errorTitle, result.ErrorMessage);
                    return;
                }

                try
                {
                    // Синхронизация нужна, если FileSystemWatcher пропустил быструю замену.
                    await RefreshAffectedContainersAsync( systemItem, targetPath, CancellationToken.None);
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

        private IEnumerable<IContainerSystemItem> EnumerateAllContainers()
        {
            var pending = new Stack<IContainerSystemItem>(
                GetDrives.OfType<IContainerSystemItem>().Reverse());
            var visited = new HashSet<IContainerSystemItem>();

            while(pending.Count > 0)
            {
                var container = pending.Pop();
                if(!visited.Add(container))
                    continue;

                yield return container;
                foreach(var child in container.Children.OfType<IContainerSystemItem>().Reverse())
                    pending.Push(child);
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

        private string CombineManagerPath(string directoryPath, string itemName)
        {
            string normalized = _fileManagerService.NormalizePath(directoryPath);
            int separatorIndex = normalized.IndexOf("://", StringComparison.Ordinal);
            if(separatorIndex <= 0)
                return Path.Combine(normalized, itemName);

            string scheme = normalized[..separatorIndex];
            string nativePath = normalized[(separatorIndex + 3)..];
            string combined = scheme.Equals("local", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(nativePath, itemName)
                : nativePath.TrimEnd('/', '\\') + "/" + itemName;
            return $"{scheme}://{combined}";
        }

        private static string GetNativePath(string path)
        {
            int separatorIndex = path.IndexOf("://", StringComparison.Ordinal);
            return separatorIndex > 0 ? path[(separatorIndex + 3)..] : path;
        }

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


    }
}
