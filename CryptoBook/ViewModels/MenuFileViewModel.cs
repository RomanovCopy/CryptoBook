using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MenuFileViewModel: ViewModelBase, IMenuFileViewModel,ICommandRegistry
    {
        private readonly IMenuFileModel menuFileModel;
        private readonly ICommandService commandService;

        public MenuFileViewModel(IMenuFileModel model, ICommandService commandService)
        {
            menuFileModel = model;
            this.commandService = commandService;
            menuFileModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            RegistryCommands();
        }


        //    IMenuFileViewModel

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


        //    ICommandRegistry


        /// <summary>
        /// регистрирует команд для внешнего использования
        /// </summary>
        public void RegistryCommands()
        {
            commandService.Register(CommandKey.menuFile_NewFile, NewFile);
            commandService.Register(CommandKey.menuFile_OpenFile, OpenFile);
            commandService.Register(CommandKey.menuFile_SaveFile, SaveFile);
            commandService.Register(CommandKey.menuFile_SaveAsFile, SaveAsFile);
            commandService.Register(CommandKey.menuFile_FileOverview, FileOverview);
            commandService.Register(CommandKey.menuFile_OpenDirectory, OpenDirectory);
            commandService.Register(CommandKey.menuFile_CloseFile, CloseFile);
            commandService.Register(CommandKey.menuFile_UpdateFile, UpdateFile);
            commandService.Register(CommandKey.menuFile_WorkingDirectorySynchronization, WorkingDirectorySynchronization);
        }
    }
}
