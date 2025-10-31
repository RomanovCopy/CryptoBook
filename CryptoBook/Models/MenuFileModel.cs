using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System.Windows.Input;

namespace CryptoBook.Models
{
    public class MenuFileModel: ViewModelBase,IMenuFileModel
    {
        private readonly IWindowManager windowManager;
        private readonly ICommandService commandService;


        public MenuFileModel(IWindowManager windowManager, ICommandService commandService)
        {
            this.windowManager = windowManager;
            this.commandService = commandService; 
        }

        public bool CanExecute_NewFile(object? obj)
        {
            return true;
        }
        public void Execute_NewFile(object? obj)
        {
            if((windowManager.CreateWindow<NewFileDialog>()).DataContext is IWindowWithId cid)
            {
                windowManager.ShowWindow<NewFileDialog>(cid.WindowId);
            }
        }

        public bool CanExecute_OpenFile(object? obj)
        {
            return true;
        }
        public void Execute_OpenFile(object? obj)
        {

        }

        public bool CanExecute_SaveFile(object? obj)
        {
            return true;
        }
        public void Execute_SaveFile(object? obj)
        {

        }

        public bool CanExecute_SaveAsFile(object? obj)
        {
            return true;
        }
        public void Execute_SaveAsFile(object? obj)
        {
        }


        public bool CanExecute_FileOverview(object? obj)
        {
            return true;
        }
        public void Execute_FileOverview(object? obj)
        {
        }

        public bool CanExecute_OpenDirectory(object? obj)
        {
            return true;
        }
        public void Execute_OpenDirectory(object? obj)
        {
        }

        public bool CanExecute_WorkingDirectorySynchronization(object? obj)
        {
            return true;
        }
        public void Execute_WorkingDirectorySynchronization(object? obj)
        {
        }

        public bool CanExecute_CloseFile(object? obj)
        {
            return true;
        }
        public void Execute_CloseFile(object? obj)
        {
        }

        public bool CanExecute_UpdateFile(object? obj)
        {
            return true;
        }
        public void Execute_UpdateFile(object? obj)
        {
        }





        public bool CanExecute_Close(object? obj)
        {
            return true;
        }
        public void Execute_Close(object? obj)
        {
        }

        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
        }

        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
        }

        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        public void Execute_Closed(object? obj)
        {
        }

    }
}
