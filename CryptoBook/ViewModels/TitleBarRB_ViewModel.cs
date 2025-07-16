using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class TitleBarRB_ViewModel: ViewModelBase, ITitleBarRB_ViewModel
    {
        private readonly ILifetimeScope scope;
        private readonly TitleBarRB_Model titleBarRB_Model;

        public TitleBarRB_ViewModel(ILifetimeScope scope)
        {
            this.scope = scope;
            titleBarRB_Model = new TitleBarRB_Model(scope);
            titleBarRB_Model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ObservableCollection<double> FontSizes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ICommand BoldCommand => boldCommand ??= new RelayCommand(titleBarRB_Model.Execute_BoldCommand, titleBarRB_Model.CanExecute_BoldCommand);
        RelayCommand boldCommand;

        public ICommand ItalicCommand => italicCommand ??= new RelayCommand(titleBarRB_Model.Execute_ItalicCommand, titleBarRB_Model.CanExecute_ItalicCommand);
        RelayCommand italicCommand;

        public ICommand UnderlineCommand => underlineCommand ??= new RelayCommand(titleBarRB_Model.Execute_UnderlineCommand, titleBarRB_Model.CanExecute_UnderlineCommand);
        RelayCommand underlineCommand;

        public ICommand ClearFormattingCommand => clearFormattingCommand ??= new RelayCommand(titleBarRB_Model.Execute_ClearFormattingCommand, titleBarRB_Model.CanExecute_ClearFormattingCommand);
        RelayCommand clearFormattingCommand;

        public ICommand InsertImageCommand => insertImageCommand ??= new RelayCommand(titleBarRB_Model.Execute_InsertImageCommand, titleBarRB_Model.CanExecute_InsertImageCommand);
        RelayCommand insertImageCommand;

        public ICommand Loaded => loaded ??= new RelayCommand(titleBarRB_Model.Execute_Loaded, titleBarRB_Model.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(titleBarRB_Model.Execute_Close, titleBarRB_Model.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(titleBarRB_Model.Execute_Closing, titleBarRB_Model.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Closed => closed ??= new RelayCommand(titleBarRB_Model.Execute_Closed, titleBarRB_Model.CanExecute_Closed);
        RelayCommand closed;
    }
}
