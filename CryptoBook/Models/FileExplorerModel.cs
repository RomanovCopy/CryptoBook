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
        private readonly IFileTemplateRegistry _fileTemplateRegistry;
        private readonly IFlowDocumentLoadService _flowDocumentLoadService;
        private readonly IRichTextBoxService _richTextBoxService;
        private readonly IFileSecurityService _fileSecurityService;
        private readonly ISecureFileValidator _secureFileValidator;
        private readonly ISystemItemCreateService _systemItemCreateService;
        private readonly IProgressDialogService _progressDialogService;

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
        private ObservableCollection<IDriveItem> _drives;
        private string _lastItemName;


        public FileExplorerModel(IFileManagerService? fileManagerService, IDriveManagerService? driveManagerService,
            IWindowManager? windowManager, IFileClipboardService fileClipboardService, IFolderPickerService folderPickerService, IMessageService messageService, IKeyProvider keyProvider, IFileTemplateRegistry fileTemplateRegistry, IFlowDocumentLoadService flowDocumentLoadService, IRichTextBoxService richTextBoxService, IFileSecurityService fileSecurityService, ISecureFileValidator secureFileValidator, ISystemItemCreateService systemItemCreateService, IProgressDialogService progressDialogService)
        {
            WindowId = Guid.NewGuid();
            _fileManagerService = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
            _driveManagerService = driveManagerService ?? throw new ArgumentNullException(nameof(driveManagerService));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _fileClipboardService = fileClipboardService ?? throw new ArgumentNullException(nameof(fileClipboardService));
            _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            _fileTemplateRegistry = fileTemplateRegistry ?? throw new ArgumentNullException(nameof(fileTemplateRegistry));
            _flowDocumentLoadService = flowDocumentLoadService ?? throw new ArgumentNullException(nameof(flowDocumentLoadService));
            _richTextBoxService = richTextBoxService ?? throw new ArgumentNullException(nameof(richTextBoxService));
            _fileSecurityService = fileSecurityService ?? throw new ArgumentNullException(nameof(fileSecurityService));
            _secureFileValidator = secureFileValidator ?? throw new ArgumentNullException(nameof(secureFileValidator));
            _systemItemCreateService = systemItemCreateService ?? throw new ArgumentNullException(nameof(systemItemCreateService));
            _progressDialogService = progressDialogService ?? throw new ArgumentNullException(nameof(progressDialogService));
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
            throw new NotImplementedException();
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
                            "Копирование файлов",
                            (progress, token) =>
                                _fileClipboardService.PasteAsync(CurrentPath, progress, token));

                    FileOperationResult? failure = results.FirstOrDefault(result => !result.Success);
                    if(failure is not null)
                    {
                        _ = await _messageService.ShowMessage("Ошибка копирования", failure.ErrorMessage);
                        return;
                    }

                    Execute_SortedCommand("Name");
                } catch(OperationCanceledException)
                {
                    // Отмена пользователем не является ошибкой копирования.
                } catch(Exception ex)
                {
                    _ = await _messageService.ShowMessage("Ошибка копирования", ex.Message);
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
                    _ = await _messageService.ShowMessage("Sorting error", fileOperationResult.ErrorMessage);
                } else
                {
                    Console.WriteLine("Не удалось распознать");
                    _ = await _messageService.ShowMessage("Sorting error", "Could not recognize column to sort");
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
                _ = await _messageService.ShowMessage("Ошибка создания директории", validationError);
                return;
            }

            string directoryPath = Path.Combine(container.FullPath, directoryName);
            if(Path.Exists(directoryPath))
            {
                _ = await _messageService.ShowMessage("Ошибка создания директории", $"Элемент с именем «{directoryName}» уже существует.");
                return;
            }

            FileOperationResult result = await _fileManagerService.CreateDirectoryAsync(container.FullPath, directoryName, CancellationToken.None);

            if(!result.Success)
            {
                _ = await _messageService.ShowMessage("Ошибка создания директории", result.ErrorMessage);
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
                var res = await _fileManagerService.RenameAsync(systemItem.FullPath, systemItem.Name, CancellationToken.None);
                if(res.Success)
                {
                    systemItem.Parent = SelectedItem;
                    if(systemItem.Parent is IContainerSystemItem parentSystemItem)
                    {
                        res = await parentSystemItem.RenameChildAsync(systemItem, systemItem.Name, CancellationToken.None);
                    }
                    if(!res.Success)
                    {
                        _ = await _messageService.ShowMessage("Rename error", res.ErrorMessage);
                        systemItem.Name = _lastItemName;
                    }
                }
                systemItem.IsEditing = false;
            } else
            {
                throw new ArgumentException("Invalid argument for RenameCommand", nameof(obj));
            }
        }
        public void Execute_MoveCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_MoveDirectory(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_RefreshCommand(object? obj)
        {
            if(obj is IContainerSystemItem container)
            {
            }
        }
        public async void Execute_CancelRenameCommand(object? obj)
        {
            var id = await _messageService.ShowMessage("Отмена операции", $"Переименование элемента отменено." + '\n' + "Вы уверены?", true);
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

            if(obj is IContainerSystemItem container)
            {
                try
                {
                    await _gate.WaitAsync(cancellationTokenSource.Token);
                    lockTaken = true;

                    SelectedItem = container;
                    CurrentPath = container.FullPath;
                    var res = await ContainerLoad(container, cancellationTokenSource.Token);
                    container.IsExpanded = true;
                    container.IsLoaded = res.Success;
                } catch(OperationCanceledException)
                {
                } finally
                {
                    if(lockTaken)
                        _gate.Release();
                }
            } else if(obj is IFileItem file)
            {
                try
                {
                    await _gate.WaitAsync(cancellationTokenSource.Token);
                    lockTaken = true;

                    if(file.IsEditing)
                        return;

                    bool isEncrypted = await _secureFileValidator.HasCryptoBookHeaderAsync(
                        file.FullPath,
                        cancellationTokenSource.Token);

                    // Для защищённого файла запрашиваем ключ до открытия потока.
                    if(isEncrypted && !_keyProvider.HasKey)
                    {
                        var keyWindowId = _windowManager.CreateWindow<KeyInputWindow>();
                        _windowManager.ShowWindowDialog(keyWindowId);

                        if(!_keyProvider.HasKey)
                            return;
                    }

                    var templates = _fileTemplateRegistry.GetAll();
                    IFileTemplate? template = templates.FirstOrDefault(t =>
                        t.Extensions.Any(ext =>
                            string.Equals(ext, file.Extension, StringComparison.OrdinalIgnoreCase)));

                    if(template != null)
                    {
                        await _progressDialogService.RunAsync(
                            "Открытие файла",
                            async (progress, token) =>
                            {
                                await using var stream = await _fileManagerService.OpenReadAsync(
                                    file.FullPath,
                                    progress,
                                    token);

                                await _flowDocumentLoadService.LoadAsync(
                                    _richTextBoxService,
                                    stream,
                                    template,
                                    token,
                                    progress);
                                return true;
                            });

                        _windowManager.CloseWindow(WindowId);
                    } else
                    {
                        _ = await _messageService.ShowMessage("File open error", $"No template found for file {file.Name}");
                    }
                } catch(OperationCanceledException)
                {
                } catch(Exception ex)
                {
                    _ = await _messageService.ShowMessage(
                        "File open error",
                        $"Failed to open file {file.Name}:\r\n{ex.Message}");
                } finally
                {
                    if(lockTaken)
                        _gate.Release();
                }
            }
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
            throw new NotImplementedException();
        }
        public void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }
        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
            WindowHeight = Properties.Settings.Default.WindowHeight_FileExplorer;
            WindowLeft = Properties.Settings.Default.WindowLeft_FileExplorer;
            WindowTop = Properties.Settings.Default.WindowTop_FileExplorer;
            WindowWidth = Properties.Settings.Default.WindowWidth_FileExplorer;
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
                error = "Имя директории не может быть пустым.";
                return false;
            }

            if(name is "." or ".." ||
               name.EndsWith(' ') ||
               name.EndsWith('.') ||
               name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
               name.Contains(Path.DirectorySeparatorChar) ||
               name.Contains(Path.AltDirectorySeparatorChar))
            {
                error = "Имя директории содержит недопустимые символы или имеет недопустимый формат.";
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
                error = $"Имя «{name}» зарезервировано операционной системой.";
                return false;
            }

            return true;
        }

        private string? GetNewFilePath(string sourcePath)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = Path.GetDirectoryName(sourcePath),
                FileName = Path.GetFileNameWithoutExtension(sourcePath) + "_Encrypted",
                DefaultExt = ".cbook",
                AddExtension = true,
                Filter = "Файлы CryptoBook (*.cbook)|*.cbook",
                FilterIndex = 1,
                OverwritePrompt = true,
                CheckPathExists = true,
                ValidateNames = true,
                Title = "Сохранить зашифрованный файл"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private async Task ExecuteFileSecurityCommandAsync(object? obj, bool decrypt)
        {
            string errorTitle = decrypt ? "Ошибка дешифрования" : "Ошибка шифрования";

            try
            {
                if(obj is IDriveItem)
                {
                    await _messageService.ShowMessage(
                        errorTitle,
                        "Нельзя зашифровать или расшифровать весь диск. Выберите файл или директорию.");
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
                        "Выбранный файл или директория не существует.");
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
                    decrypt ? "Дешифрование" : "Шифрование",
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
                    await _messageService.ShowMessage( "Ошибка обновления проводника",
                        $"Операция завершена успешно, но не удалось обновить представление файлов:\r\n{ex.Message}");
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
            string subject = isDirectory ? "исходные файлы" : "исходный файл";
            string verb = isDirectory ? "будут заменены" : "будет заменён";
            string replacement = decrypt
                ? isDirectory ? "дешифрованными копиями" : "дешифрованной копией"
                : isDirectory ? "зашифрованными копиями" : "зашифрованной копией";
            string keyWarning = decrypt
                ? string.Empty
                : "\r\n\r\nОткрытие без ключа станет невозможным.";

            string warning =
                $"Заменить {subject}?\r\n\r\n" +
                $"{char.ToUpperInvariant(subject[0])}{subject[1..]} {verb} {replacement}.\r\n" +
                $"{sourcePath}{keyWarning}";

            Guid messageId = await _messageService.ShowMessage(
                "Перезапись исходного элемента",
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
                FileName = Path.GetFileNameWithoutExtension(sourcePath) + "_Decrypted",
                AddExtension = false,
                Filter = "Все файлы (*.*)|*.*",
                FilterIndex = 1,
                OverwritePrompt = true,
                CheckPathExists = true,
                ValidateNames = true,
                Title = "Сохранить расшифрованный файл"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }


    }
}
