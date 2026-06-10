using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Views;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
            IWindowManager? windowManager, IFileClipboardService fileClipboardService, IFolderPickerService folderPickerService, IMessageService messageService)
        {
            WindowId = Guid.NewGuid();
            _fileManagerService = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
            _driveManagerService = driveManagerService ?? throw new ArgumentNullException(nameof(driveManagerService));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _fileClipboardService = fileClipboardService ?? throw new ArgumentNullException(nameof(fileClipboardService));
            _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
            GetDrives = _driveManagerService.WritableDrives;
        }


        public bool CanExecute_BackCommand(object? obj)
        {
            return SelectedItem?.Parent is not null;
        }
        public bool CanExecute_CutCommand(object? obj)
        {
            return obj is IList { Count: > 0 };
        }
        public bool CanExecute_CopyCommand(object? obj)
        {
            return obj is IList { Count: > 0 };
        }
        public bool CanExecute_PasteCommand(object? obj)
        {
            if(!string.IsNullOrEmpty(CurrentPath))
            {
                return _fileClipboardService.GetData().SourcePaths.Count > 0;
            }
            return false;
        }
        public bool CanExecute_DeleteCommand(object? obj)
        {
            return obj is ISystemItem;
        }
        public bool CanExecute_EncryptCommand(object? obj)
        {
            return obj is string name && !string.IsNullOrWhiteSpace(name) &&
            SelectedItem is IContainerSystemItem item && item.Children.Count > 1;
        }
        public bool CanExecute_CreateFileCommand(object? obj)
        {
            return true;
        }
        public bool CanExecute_CreateDirectoryCommand(object? obj)
        {
            return true;
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
                var systemItems = new List<string>();
                foreach(var item in list)
                {
                    if(item is ISystemItem systemItem && systemItem.FullPath is not null)
                    {
                        systemItems.Add(systemItem.FullPath);
                    }
                }
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
                var systemItems = new List<string>();
                foreach(var item in list)
                {
                    if(item is ISystemItem systemItem && systemItem.FullPath is not null)
                    {
                        systemItems.Add(systemItem.FullPath);
                    }
                }
                _fileClipboardService.SetCopy(systemItems);
            } else
            {
                throw new ArgumentException("Invalid argument for CopyCommand", nameof(obj));
            }
        }
        public void Execute_PasteCommand(object? obj)
        {
            if(!string.IsNullOrEmpty(CurrentPath) && _fileClipboardService.GetData().SourcePaths.Count > 0)
            {
                var sourcePaths = _fileClipboardService.GetData().SourcePaths;
                _ = Task.Run(async () =>
                {
                    foreach(var sourcePath in sourcePaths)
                    {
                        var fileName = System.IO.Path.GetFileName(sourcePath);
                        await _fileClipboardService.PasteAsync(CurrentPath, null, CancellationToken.None);
                    }
                    Execute_EncryptCommand("Name");
                });
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
        public async void Execute_EncryptCommand(object? obj)
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
        public void Execute_CreateFileCommand(object? obj)
        {
            var id = _windowManager.CreateWindow<NewFileDialog>();
            _windowManager.ShowWindow(id);
            Execute_EncryptCommand("Name");
        }
        public void Execute_CreateDirectoryCommand(object? obj)
        {

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
            _cancellationTokenSource = new CancellationTokenSource();
            await _gate.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                switch(obj)
                {
                    case IContainerSystemItem container:
                    {
                        SelectedItem = container;
                        CurrentPath = container.FullPath;
                        container.IsExpanded = !container.IsExpanded;
                        if(container.IsExpanded)
                        {
                            var res = await ContainerLoad(container, _cancellationTokenSource.Token);
                            container.IsLoaded = res.Success;
                        }
                        break;
                    }
                    case IFileItem file:
                    {
                        break;
                    }
                    default:
                    return;
                }
            } catch
            {
                _cancellationTokenSource.Cancel();
            } finally
            {
                _gate.Release();
            }

        }
        public async void Execute_ListViewItemDoubleClickCommand(object? obj)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            if(obj is IContainerSystemItem container)
            {
                await _gate.WaitAsync(_cancellationTokenSource.Token);
                try
                {
                    SelectedItem = container;
                    CurrentPath = container.FullPath;
                    var res = await ContainerLoad(container, _cancellationTokenSource.Token);
                    container.IsExpanded = true;
                    container.IsLoaded = res.Success;
                } catch
                {
                    _cancellationTokenSource.Cancel();
                } finally
                {
                    _gate.Release();
                }
            } else if(obj is IFileItem file)
            {
                await _gate.WaitAsync(_cancellationTokenSource.Token);
                try
                {
                    var stream = await _fileManagerService.OpenReadAsync(file.FullPath, _cancellationTokenSource.Token);

                } catch
                {
                    _cancellationTokenSource.Cancel();
                } finally
                {
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
            return await Task.Run(async () =>
            {
                FileOperationResult result = container.IsLoaded ? FileOperationResult.Ok() : FileOperationResult.Fail($"Error reading directory {container.FullPath}");
                if(!result.Success)
                {
                    var children = _fileManagerService.BrowseAsync(container.FullPath, token, IsHiddenFilesVisible).Result;
                    if(children is not null)
                    {
                        if(!container.IsLoaded)
                        {
                            result = await container.AddChildAsync(children, (x) => x.FullPath, token);
                        }
                    }
                }
                return result;
            }, token);
        }

    }
}
