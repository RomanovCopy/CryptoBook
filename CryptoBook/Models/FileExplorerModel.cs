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

        private readonly IFileManagerService _fileManagerService;
        private readonly IWindowManager _windowManager;
        private readonly IDriveManagerService _driveManagerService;
        private readonly ISystemItemCreateService _itemCreateService;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

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
        }

        private async void update(object o)
        {
            //_files.Clear();
            //var files = await _fileManagerService.BrowseAsync(path, _cancellationTokenSource.Token, isHidden);
            //if(files == null) return;
            //foreach(var file in files)
            //{
            //    _files.Add(file);
            //}
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
            return true;
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

        public async void Execute_TreeViewItemSelectedCommand(object? obj)
        {
            if(_cancellationTokenSource!=null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            _cancellationTokenSource = new CancellationTokenSource();
            switch(obj)
            {
                case IDirectoryItem directory:
                {
                    CurrentPath = directory.FullPath;
                    var item = directory;
                    if(!item.IsLoaded && item is IContainerSystemItem container)
                    {
                        container.ClearChildren();
                        var children = await _fileManagerService.BrowseAsync(directory.FullPath, _cancellationTokenSource.Token, IsHiddenFilesVisible);
                        if(children is not null)
                        {
                            foreach(var child in children)
                            {
                                await container.AddChildAsync(child,(x)=>x.FullPath, _cancellationTokenSource.Token);
                            }
                        }
                        item.IsLoaded = true;
                    }
                    break;
                }
                case IDriveItem drive:
                {
                    CurrentPath = drive.RootDirectory;
                    if(!drive.IsLoaded && drive is IContainerSystemItem container)
                    {
                        container.ClearChildren();
                        var children = await _fileManagerService.BrowseAsync(drive.RootDirectory, _cancellationTokenSource.Token, IsHiddenFilesVisible);
                        if(children != null)
                        {
                            foreach(var child in children)
                            {
                                await container.AddChildAsync(child, (x) => x.FullPath, _cancellationTokenSource.Token);
                            }
                        }
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
            if((obj is IContainerSystemItem))
            {
                var files = await _fileManagerService.BrowseAsync(CurrentPath, _cancellationTokenSource.Token, IsHiddenFilesVisible);
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
