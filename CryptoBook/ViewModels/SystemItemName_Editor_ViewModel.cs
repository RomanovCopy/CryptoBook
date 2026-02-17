using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class SystemItemName_Editor_ViewModel :ViewModelBase, ISystemItemName_Editor_ViewModel
    {
        private readonly ISystemItemName_Editor_Model model;

        public Guid WindowId => model.WindowId;

        public double WindowWidth { get => model.WindowWidth; set => model.WindowWidth = value; }
        public double WindowHeight { get => model.WindowHeight; set => model.WindowHeight = value; }
        public double WindowTop { get => model.WindowTop; set => model.WindowTop = value; }
        public double WindowLeft { get => model.WindowLeft; set => model.WindowLeft = value; }
        public WindowState WindowState { get => model.WindowState; set => model.WindowState = value; }

        public string OldName => model.OldName;
        public string OldExtension => model.OldExtension;

        public string NewName { get => model.NewName; set => model.NewName = value; }
        public string NewExtension { get => model.NewExtension; set => model.NewExtension = value; }

        public SystemItemName_Editor_ViewModel(ISystemItemName_Editor_Model model)
        {
            this.model = model;
        }

        public ICommand SaveCommand => saveCommand ??= new RelayCommand(model.Execute_SaveCommand, model.CanExecute_SaveCommand);
        RelayCommand saveCommand;
        public ICommand RestoreCommand => restoreCommand ??= new RelayCommand(model.Execute_RestoreCommand, model.CanExecute_RestoreCommand);
        RelayCommand restoreCommand;


        public ICommand Loaded => loadedCommand ??= new RelayCommand(model.Execute_Loaded, model.CanExecute_Loaded);
        RelayCommand loadedCommand;
        public ICommand Close => closeCommand ??= new RelayCommand(model.Execute_Close, model.CanExecute_Close);
        RelayCommand closeCommand;
        public ICommand Closing => closingCommand ??= new RelayCommand(model.Execute_Closing, model.CanExecute_Closing);
        RelayCommand closingCommand;
        public ICommand Closed => closedCommand ??= new RelayCommand(model.Execute_Closed, model.CanExecute_Closed);
        RelayCommand closedCommand;


    }
}
