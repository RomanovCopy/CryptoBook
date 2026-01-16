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

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public Guid WindowId { get => _windowId; private set => SetProperty(ref _windowId, value); }
        private Guid _windowId;
        public bool IsHiddenFilesVisible { get => _isHiddenFilesVisible; set => SetProperty(ref _isHiddenFilesVisible, value); }
        private bool _isHiddenFilesVisible;
        public DriveInfoEx SelectedDrive { get => _selectedDrive; set => SetProperty(ref _selectedDrive, value); }
        private DriveInfoEx _selectedDrive;
        public string CurrentPath { get => _currentPath; set => SetProperty(ref _currentPath, value); }
        private string _currentPath;
        public ReadOnlyObservableCollection<IFileItem> GetFiles { get; private set; }
        private ObservableCollection<IFileItem> _files;
        public ReadOnlyObservableCollection<DriveInfoEx> GetDrives { get; private set; }
        private ObservableCollection<DriveInfoEx> _drives;
        public ReadOnlyObservableCollection<IDirectoryItem> GetDirectories { get; private set; }
        private ObservableCollection<IDirectoryItem> _directories;



        public FileExplorerModel(IFileManagerService fileManagerService, IDriveManagerService driveManagerService, IWindowManager windowManager)
        {
            WindowId = Guid.NewGuid();
            _fileManagerService = fileManagerService;
            _driveManagerService = driveManagerService;
            _windowManager = windowManager;
            GetDrives = _driveManagerService.WritableDrives;
            _isHiddenFilesVisible = true;
            //_drives = [];
            //GetDrives=new(_drives);
            _directories = [];
            GetDirectories=new(_directories);
            _files = [];
            GetFiles=new(_files);

        }

        private void update()
        {
            foreach(var file in GetFiles)
            {
                if(file.IsDirectory)
                {
                    
                } else
                {

                }
            }
        }

        private IDirectoryItem ToDirectoryItem(IFileItem file)
        {
            var directory = new DirectoryItem(file.FullPath);
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

        public async void Execute_TreeViewItemSelectedCommand(object? obj)
        {
            if(obj is DriveInfoEx drive)
            {
                SelectedDrive = drive;
                CurrentPath = drive.RootDirectory;
            } else if(obj is IDirectoryItem dir)
            {
                CurrentPath = dir.FullPath;
            }
            var files = await _fileManagerService.BrowseAsync(CurrentPath, _cancellationTokenSource.Token, IsHiddenFilesVisible);
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
