using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MenuFileViewModel: ViewModelBase, IMenuFileViewModel, ICommandRegistry
    {
        private readonly IMenuFileModel menuFileModel;
        private readonly ICommandService commandService;

        public MenuFileViewModel(IMenuFileModel model, ICommandService commandService)
        {
            menuFileModel = model;
            this.commandService = commandService;
            menuFileModel.PropertyChanged += (_, args) =>
            {
                OnPropertyChanged(args.PropertyName);
                saveFile?.RaiseCanExecuteChanged();
                saveAsFile?.RaiseCanExecuteChanged();
                closeFile?.RaiseCanExecuteChanged();
            };
            RegistryCommands();
        }


        //    IMenuFileViewModel

        public ICommand NewFile => newFile ??= new RelayCommand(menuFileModel.Execute_NewFile, menuFileModel.CanExecute_NewFile);
        RelayCommand newFile;

        public ICommand OpenFile => openFile ??= new RelayCommand(menuFileModel.Execute_OpenFile, menuFileModel.CanExecute_OpenFile);
        RelayCommand openFile;

        public ICommand SaveFile => saveFile ??=
            new AsyncRelayCommand(
                menuFileModel.Execute_SaveFileAsync,
                menuFileModel.CanExecute_SaveFile);
        AsyncRelayCommand? saveFile;

        public ICommand SaveAsFile => saveAsFile ??=
            new AsyncRelayCommand(
                menuFileModel.Execute_SaveAsFileAsync,
                menuFileModel.CanExecute_SaveAsFile);
        AsyncRelayCommand? saveAsFile;

        public ICommand FileOverview => fileOverview ??= new RelayCommand(menuFileModel.Execute_FileOverview, menuFileModel.CanExecute_FileOverview);
        RelayCommand fileOverview;

        public ICommand OpenDirectory => openDirectory ??= new RelayCommand(menuFileModel.Execute_OpenDirectory, menuFileModel.CanExecute_OpenDirectory);
        RelayCommand openDirectory;
        public ICommand CloseFile => closeFile ??=
            new AsyncRelayCommand(
                menuFileModel.Execute_CloseFileAsync,
                menuFileModel.CanExecute_CloseFile);
        AsyncRelayCommand? closeFile;
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
