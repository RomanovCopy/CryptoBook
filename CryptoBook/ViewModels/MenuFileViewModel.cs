using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

namespace CryptoBook.ViewModels
{
    public class MenuFileViewModel:ViewModelBase, IMenuFileViewModel
    {
        private readonly MenuFileModel menuFileModel;
        public MenuFileViewModel()
        {
            menuFileModel = new MenuFileModel();
            menuFileModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand NewFile => newFile ??= new RelayCommand(menuFileModel.Execute_NewFile, menuFileModel.CanExecute_NewFile);
        RelayCommand newFile;

        public ICommand OpenFile => openFile ??= new RelayCommand(menuFileModel.Execute_OpenFile, menuFileModel.CanExecute_OpenFile);
        RelayCommand openFile;

        public ICommand SaveFile => saveFile ??= new RelayCommand(menuFileModel.Execute_SaveFile, menuFileModel.CanExecute_SaveFile);
        RelayCommand saveFile;

        public ICommand SaveAsFile => saveAsFile ??= new RelayCommand(menuFileModel.Execute_SaveAsFile, menuFileModel.CanExecute_SaveAsFile);
        RelayCommand saveAsFile;

        public ICommand FileOverview => fileOverview ??= new RelayCommand(menuFileModel.Execute_FileOverview, menuFileModel.CanExecute_FileOverview);
        RelayCommand fileOverview;

        public ICommand OpenDirectory => openDirectory ??= new RelayCommand(menuFileModel.Execute_OpenDirectory, menuFileModel.CanExecute_OpenDirectory);
        RelayCommand openDirectory;
        public ICommand CloseFile => closeFile ??= new RelayCommand(menuFileModel.Execute_CloseFile, menuFileModel.CanExecute_CloseFile);
        RelayCommand closeFile;
        public ICommand UpdateFile => updateFile ??= new RelayCommand(menuFileModel.Execute_UpdateFile, menuFileModel.CanExecute_UpdateFile);
        RelayCommand updateFile;

        public ICommand WorkingDirectorySynchronization => workingDirectorySynchronization ??= new RelayCommand(menuFileModel.Execute_WorkingDirectorySynchronization, menuFileModel.CanExecute_WorkingDirectorySynchronization);
        RelayCommand workingDirectorySynchronization;


        public ICommand Loaded { get; }
        public ICommand Close { get; }
        public ICommand Closing { get; }

        public event EventHandler RequestClose;
    }
}
