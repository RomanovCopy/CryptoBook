using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
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
        private readonly IFileLauncherService _fileLauncherService;
        private readonly IStockIconService _stockIconService;
        private readonly IFileClipboardService _fileClipboardService;

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


        public FileExplorerModel(IFileManagerService? fileManagerService, IDriveManagerService? driveManagerService,
            IWindowManager? windowManager, IFileLauncherService? fileLauncherService, IStockIconService stockIconService,
            IFileClipboardService fileClipboardService)
        {
            WindowId = Guid.NewGuid();
            _fileManagerService = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
            _driveManagerService = driveManagerService ?? throw new ArgumentNullException(nameof(driveManagerService));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _fileLauncherService = fileLauncherService ?? throw new ArgumentNullException(nameof(fileLauncherService));
            _stockIconService = stockIconService ?? throw new ArgumentNullException(nameof(stockIconService));
            _fileClipboardService = fileClipboardService ?? throw new ArgumentNullException(nameof(fileClipboardService));
            GetDrives = _driveManagerService.WritableDrives;
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
            throw new NotImplementedException();
        }

        public bool CanExecute_CreateFileCommand(object? obj)
        {
            return false;
        }

        public bool CanExecute_CreateDirectoryCommand(object? obj)
        {
            return true;
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
                        //var destinationPath = System.IO.Path.Combine(CurrentPath, fileName);
                        await _fileClipboardService.PasteAsync(CurrentPath, null, CancellationToken.None);
                    }
                });
            } else
            {
                throw new ArgumentException("Invalid argument for PasteCommand", nameof(obj));
            }
        }

        public void Execute_DeleteCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_CreateFileCommand(object? obj)
        {
            var id = _windowManager.CreateWindow<NewFileDialog>();
            _windowManager.ShowWindow(id);
        }

        public void Execute_CreateDirectoryCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_RenameCommand(object? obj)
        {
            if(obj is ISystemItem systemItem)
            {
                var id = _windowManager.CreateWindow<SystemItemName_Editor>();
                _windowManager.ShowWindow(id);
            } else
            {
                throw new ArgumentException("Invalid argument for RenameCommand", nameof(obj));
            }
        }

        public void Execute_DeleteFile(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_DeleteDirectory(object? obj)
        {
            throw new NotImplementedException();
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
                        ContainerLoad(container, _cancellationTokenSource.Token);
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
            if(obj is IContainerSystemItem container)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                await _gate.WaitAsync(_cancellationTokenSource.Token);
                try
                {
                    SelectedItem = container;
                    CurrentPath = container.FullPath;
                    ContainerLoad(container, _cancellationTokenSource.Token);
                    container.IsExpanded = !container.IsExpanded || true;
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
            //if(obj is ISystemItem item)
            //{
            //    SelectedItem=item;
            //}
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


        private void ContainerLoad(IContainerSystemItem container, CancellationToken token)
        {
            if(!container.IsLoaded)
            {
                _ = Task.Run(async () =>
                {
                    await container.ClearChildrenAsync();
                    var children = await _fileManagerService.BrowseAsync(container.FullPath, token, IsHiddenFilesVisible);
                    if(children is not null)
                    {
                        await container.AddChildAsync(children, (x) => x.FullPath, token);
                    }
                    container.IsLoaded = true;
                }, token);
            } else
            {
                _ = Task.Run(async () =>
                {
                    var children = await _fileManagerService.BrowseAsync(container.FullPath, token, IsHiddenFilesVisible);
                    if(children is not null)
                    {
                        await container.SyncCollectionsAsync(children, (x) => x.FullPath, null, token);
                    }
                }, token);
            }
        }

    }
}
