using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MenuEncryptionViewModel: ViewModelBase, IMenuEncryptionViewModel
    {
        private readonly MenuEncryptionModel menuEncryptionModel;

        public MenuEncryptionViewModel()
        {
            menuEncryptionModel = new MenuEncryptionModel();
            menuEncryptionModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand DeleteKey => deleteKey ??= new RelayCommand(menuEncryptionModel.Execute_DeleteKey, menuEncryptionModel.CanExecute_DeleteKey);
        RelayCommand deleteKey;

        public ICommand InstalKey => instalKey ??= new RelayCommand(menuEncryptionModel.Execute_InstalKey, menuEncryptionModel.CanExecute_InstalKey);
        RelayCommand instalKey;

        public ICommand EncryptionPanel => encryptionPanel ??= new RelayCommand(menuEncryptionModel.Execute_EncryptionPanel, menuEncryptionModel.CanExecute_EncryptionPanel);
        RelayCommand encryptionPanel;

        public ICommand Loaded => loaded ??= new RelayCommand(menuEncryptionModel.Execute_Loaded, menuEncryptionModel.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(menuEncryptionModel.Execute_Close, menuEncryptionModel.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(menuEncryptionModel.Execute_Closing, menuEncryptionModel.CanExecute_Closing);
        RelayCommand closing;

    }
}
