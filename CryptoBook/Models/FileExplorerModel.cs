using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
        private readonly ISystemItemCreateService _itemCreateService;

        private CancellationTokenSource _cancellationTokenSource = new();

        public double WindowWidth { get => _windowWidth; set => SetProperty(ref _windowWidth, value); }
        private double _windowWidth;
        public double WindowHeight { get => _windowHeight; set => SetProperty(ref _windowHeight, value); }
        private double _windowHeight;
        public double WindowTop { get => _windowTop; set => SetProperty(ref _windowTop, value); }
        private double _windowTop;
        public double WindowLeft { get => _windowLeft; set => SetProperty(ref _windowLeft, value); }
        private double _windowLeft;
        public WindowState WindowState { get =>_windowState; set => SetProperty(ref _windowState, value); }
        private WindowState _windowState;

        public double LeftColumnPercent { get => _leftCololumnPercent; set => SetProperty(ref _leftCololumnPercent, value); }
        private double _leftCololumnPercent;
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
            IWindowManager? windowManager, ISystemItemCreateService? itemCreateService)
        {
            WindowId = Guid.NewGuid();
            _fileManagerService = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
            _driveManagerService = driveManagerService ?? throw new ArgumentNullException(nameof(driveManagerService));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _itemCreateService = itemCreateService ?? throw new ArgumentNullException(nameof(itemCreateService));
            GetDrives = _driveManagerService.WritableDrives;
            _isHiddenFilesVisible = true;
            LeftColumnPercent = 0.2;
            WindowWidth = 600;
        }

        public bool CanExecute_CutCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_CopyCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_PasteCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_DeleteCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_CreateFileCommand(object? obj)
        {
            return true;
        }

        public bool CanExecute_CreateDirectoryCommand(object? obj)
        {
            return true;
        }
        public bool CanExecute_RenameCommand(object? obj)
        {
            throw new NotImplementedException();
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




        public void Execute_CutCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_CopyCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_PasteCommand(object? obj)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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

        bool _expanded = false;

        public async void Execute_TreeViewItemSelectedCommand(object? obj)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            await _gate.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                switch(obj)
                {
                    case IDirectoryItem directory:
                    {
                        CurrentPath = directory.FullPath;
                        var item = directory;
                        if(!item.IsLoaded && item is IContainerSystemItem container)
                        {
                            await container.ClearChildrenAsync();
                            var children = await _fileManagerService.BrowseAsync(directory.FullPath, _cancellationTokenSource.Token, IsHiddenFilesVisible);
                            if(children is not null)
                            {
                                await container.AddChildAsync(children, (x) => x.FullPath, _cancellationTokenSource.Token);
                            }
                            item.IsLoaded = true;
                        }
                        break;
                    }
                    case IDriveItem drive:
                    {
                        CurrentPath = drive.RootDirectory;
                        var item = drive;
                        if(!item.IsLoaded && item is IContainerSystemItem container)
                        {
                            await container.ClearChildrenAsync();
                            var children = await _fileManagerService.BrowseAsync(CurrentPath, _cancellationTokenSource.Token, IsHiddenFilesVisible);
                            if(children != null)
                            {
                                await container.AddChildAsync(children, (x) => x.FullPath, _cancellationTokenSource.Token);
                            }
                            item.IsLoaded = true;
                        }
                        break;
                    }
                    case IFileItem file:
                    {
                        CurrentPath = System.IO.Path.GetDirectoryName(file.FullPath) ?? string.Empty;
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
            throw new NotImplementedException();
        }
        public void Execute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }



        public bool CanExecute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }


    }
}
