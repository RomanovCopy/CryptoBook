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
    public class MenuContentViewModel: ViewModelBase, IMenuContentViewModel
    {
        private readonly MenuContentModel menuContentModel;

        public MenuContentViewModel()
        {
            menuContentModel = new MenuContentModel();
            menuContentModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand Reading => reading ??= new RelayCommand(menuContentModel.Execute_Reading, menuContentModel.CanExecute_Reading);
        RelayCommand reading;

        public ICommand InsertImage => insertImage ??= new RelayCommand(menuContentModel.Execute_InsertImage, menuContentModel.CanExecute_InsertImage);
        RelayCommand insertImage;

        public ICommand InsertText => insertText ??= new RelayCommand(menuContentModel.Execute_InsertText, menuContentModel.CanExecute_InsertText);
        RelayCommand insertText;

        public ICommand OpenDocumentTree => openDocumentTree ??= new RelayCommand(menuContentModel.Execute_OpenDocumentTree, menuContentModel.CanExecute_OpenDocumentTree);
        RelayCommand openDocumentTree;

        public ICommand MediaPlayer => mediaPlayer ??= new RelayCommand(menuContentModel.Execute_MediaPlayer, menuContentModel.CanExecute_MediaPlayer);
        RelayCommand mediaPlayer;

        public ICommand Loaded => loaded ??= new RelayCommand(menuContentModel.Execute_Loaded, menuContentModel.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(menuContentModel.Execute_Close, menuContentModel.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(menuContentModel.Execute_Closing, menuContentModel.CanExecute_Closing);
        RelayCommand closing;

        public event EventHandler RequestClose;
    }
}
