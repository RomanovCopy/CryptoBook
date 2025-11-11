using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class NewFileDialogViewModel: ViewModelBase, INewFileDialogViewModel,ICommandRegistry,ICloseable
    {
        private readonly INewFileDialogModel newFileDialogModel;
        private readonly ICommandService commandService;

        public event EventHandler RequestClose;

        public Guid WindowId { get => newFileDialogModel.WindowId; }


        public IReadOnlyList<IFileTemplate> Templates { get => newFileDialogModel.Templates;}

        public IFileTemplate? SelectedTemplate { get => newFileDialogModel.SelectedTemplate; set => newFileDialogModel.SelectedTemplate = value; }

        public string FileName { get => newFileDialogModel.FileName; set => newFileDialogModel.FileName = value; }

        public IfExistsMode IfExists { get => newFileDialogModel.IfExists; set => newFileDialogModel.IfExists = value; }

        public string TargetDirectory { get => newFileDialogModel.TargetDirectory; set => newFileDialogModel.TargetDirectory=value; }
        public string ErrorMessage { get => newFileDialogModel.ErrorMessage ?? string.Empty; }
        public bool CanWrite { get => newFileDialogModel.CanWrite; set => newFileDialogModel.CanWrite = value; }
        public bool ShowHiddenFiles { get => newFileDialogModel.ShowHiddenFiles; set => newFileDialogModel.ShowHiddenFiles = value; }
        public bool CreateDirectoryIfMissing { get => newFileDialogModel.CreateDirectoryIfMissing; set => newFileDialogModel.CreateDirectoryIfMissing = value; }


        public NewFileDialogViewModel(INewFileDialogModel newFileDialogModel, ICommandService commandService)
        {
            this.newFileDialogModel = newFileDialogModel;
            this.commandService = commandService;
            this.newFileDialogModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName??string.Empty);
        }


        public ICommand InitSuggested => initSuggested ??= new RelayCommand(newFileDialogModel.Execute_InitSuggested, newFileDialogModel.CanExceute_InitSuggested);
        RelayCommand? initSuggested;

        public ICommand Browse => browse ??= new RelayCommand(newFileDialogModel.Execute_Browse, newFileDialogModel.CanExecute_Browse);
        RelayCommand? browse;

        public ICommand CreateDirectory => createDirectory ??= new RelayCommand(newFileDialogModel.Execute_CreateDirectory, newFileDialogModel.CanExecute_CreateDirectory);
        RelayCommand? createDirectory;

        public ICommand Create => create ??= new RelayCommand(newFileDialogModel.Execute_Create, newFileDialogModel.CanExecute_Create);
        RelayCommand? create;

        public ICommand Cancel => cancel ??= new RelayCommand(newFileDialogModel.Execute_Cancel, newFileDialogModel.CanExecute_Cancel);
        RelayCommand? cancel;



        public ICommand Loaded =>loaded??=new RelayCommand(newFileDialogModel.Execute_Loaded, newFileDialogModel.CanExecute_Loaded);
        RelayCommand? loaded;

        public ICommand Close => close??=new RelayCommand(newFileDialogModel.Execute_Close, newFileDialogModel.CanExecute_Close);
        RelayCommand? close;

        public ICommand Closing => closing??=new RelayCommand(newFileDialogModel.Execute_Closing, newFileDialogModel.CanExecute_Closing);
        RelayCommand? closing;

        public ICommand Closed => closed??=new RelayCommand(newFileDialogModel.Execute_Closed, newFileDialogModel.CanExecute_Closed);
        RelayCommand? closed;

        public void RegistryCommands()
        {
            commandService.Register(CommandKey.NewFileDialog_Create, Browse);
        }

    }
}
