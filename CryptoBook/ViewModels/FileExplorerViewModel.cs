using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class FileExplorerViewModel: ViewModelBase, IFileExplorerViewModel
    {
        private readonly IFileExplorerModel _fileExplorerModel;

        public bool IsHiddenFilesVisible { get => _fileExplorerModel.IsHiddenFilesVisible; set => _fileExplorerModel.IsHiddenFilesVisible=value; }
        public object SelectedItem { get => _fileExplorerModel.SelectedItem; set => _fileExplorerModel.SelectedItem=value; }
        public DriveItem SelectedDrive{ get=> _fileExplorerModel.SelectedDrive; set => _fileExplorerModel.SelectedDrive=value; }
        public ReadOnlyObservableCollection<IFileSystemItem> GetFiles => _fileExplorerModel.GetFiles;
        public ReadOnlyObservableCollection<IDirectoryItem> GetDirectories => _fileExplorerModel.GetDirectories;
        public ReadOnlyObservableCollection<DriveItem> GetDrives => _fileExplorerModel.GetDrives;
        public string CurrentPath { get => _fileExplorerModel.CurrentPath; set => _fileExplorerModel.CurrentPath=value; }
        public Guid WindowId => _fileExplorerModel.WindowId;


        public FileExplorerViewModel( IFileExplorerModel fileExplorerModel)
        {
            _fileExplorerModel = fileExplorerModel ?? throw new ArgumentNullException(nameof(fileExplorerModel));
            _fileExplorerModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand CutCommand => _cutCommand ??= new RelayCommand(_fileExplorerModel.Execute_CutCommand, _fileExplorerModel.CanExecute_CutCommand);
        RelayCommand _cutCommand;

        public ICommand CopyCommand => _copyCommand ??= new RelayCommand(_fileExplorerModel.Execute_CopyCommand, _fileExplorerModel.CanExecute_CopyCommand);
        RelayCommand _copyCommand;

        public ICommand PasteCommand => _pasteCommand ??= new RelayCommand(_fileExplorerModel.Execute_PasteCommand, _fileExplorerModel.CanExecute_PasteCommand);
        RelayCommand _pasteCommand;

        public ICommand DeleteCommand => _deleteCommand ??= new RelayCommand(_fileExplorerModel.Execute_DeleteCommand, _fileExplorerModel.CanExecute_DeleteCommand);
        RelayCommand _deleteCommand;

        public ICommand CreateFileCommand => _createFileCommand ??= new RelayCommand(_fileExplorerModel.Execute_CreateFileCommand, _fileExplorerModel.CanExecute_CreateFileCommand);
        RelayCommand _createFileCommand;

        public ICommand CreateDirectoryCommand => _createDirectoryCommand ??= new RelayCommand(_fileExplorerModel.Execute_CreateDirectoryCommand, _fileExplorerModel.CanExecute_CreateDirectoryCommand);
        RelayCommand _createDirectoryCommand;

        public ICommand RenameCommand => _renameFileCommand ??= new RelayCommand(_fileExplorerModel.Execute_RenameCommand, _fileExplorerModel.CanExecute_RenameCommand);
        RelayCommand _renameFileCommand;

        public ICommand MoveCommand => _moveCommand ??= new RelayCommand(_fileExplorerModel.Execute_MoveCommand, _fileExplorerModel.CanExecute_MoveCommand);
        RelayCommand _moveCommand;

        public ICommand TreeViewItemSelectedCommand => _treeViewItemSelectedCommand ??= new RelayCommand(_fileExplorerModel.Execute_TreeViewItemSelectedCommand, _fileExplorerModel.CanExecute_TreeViewItemSelectedCommand);
        RelayCommand _treeViewItemSelectedCommand;


        public ICommand Loaded => _loadedCommand ??= new RelayCommand(_fileExplorerModel.Execute_Loaded, _fileExplorerModel.CanExecute_Loaded);
        RelayCommand _loadedCommand;

        public ICommand Close => _closeCommand ??= new RelayCommand(_fileExplorerModel.Execute_Close, _fileExplorerModel.CanExecute_Close);
        RelayCommand _closeCommand;

        public ICommand Closing => _closingCommand ??= new RelayCommand(_fileExplorerModel.Execute_Closing, _fileExplorerModel.CanExecute_Closing);
        RelayCommand _closingCommand;

        public ICommand Closed => _closedCommand ??= new RelayCommand(_fileExplorerModel.Execute_Closed, _fileExplorerModel.CanExecute_Closed);
        RelayCommand _closedCommand;

    }
}
