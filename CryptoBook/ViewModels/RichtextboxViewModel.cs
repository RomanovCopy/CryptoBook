using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class RichtextboxViewModel: ViewModelBase, IRichtextboxViewModel
    {
        private readonly RichtextboxModel richtextboxModel;

        public RichtextboxViewModel()
        {
            richtextboxModel = new();
            richtextboxModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand Loaded => loaded ??=
            new RelayCommand(richtextboxModel.Execute_Loaded, richtextboxModel.CanExecute_Loaded);
        private RelayCommand? loaded;

        public ICommand Close => close ??=
            new RelayCommand(richtextboxModel.Execute_Close, richtextboxModel.CanExecute_Close);
        private RelayCommand? close;

        public ICommand Closing => closing ??=
            new RelayCommand(richtextboxModel.Execute_Closing, richtextboxModel.CanExecute_Closing);
        private RelayCommand? closing;

        public ICommand Closed => closed ??=
            new RelayCommand(richtextboxModel.Execute_Closed, richtextboxModel.CanExecute_Closed);
        private RelayCommand? closed;
    }
}
