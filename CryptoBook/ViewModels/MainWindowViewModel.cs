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
    public class MainWindowViewModel:ViewModelBase, IMainWindowViewModel
    {
        private readonly MainWindowModel mainWindowModel;


        public MainWindowViewModel()
        {
            mainWindowModel = new();
            mainWindowModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }



        ICommand IMainWindowViewModel.FrameListAddPage()
        {
            throw new NotImplementedException();
        }

        ICommand IMainWindowViewModel.FrameListRemovePage()
        {
            throw new NotImplementedException();
        }

        ICommand IMainWindowViewModel.Frmelist_GoForward()
        {
            throw new NotImplementedException();
        }

        ICommand IMainWindowViewModel.Frmelist_GoBack()
        {
            throw new NotImplementedException();
        }

        ICommand IMainWindowViewModel.PageClosed()
        {
            throw new NotImplementedException();
        }

        ICommand IViewModel.Loaded(object obj)
        {
            throw new NotImplementedException();
        }

        ICommand IViewModel.Closed(object obj)
        {
            throw new NotImplementedException();
        }

        ICommand IViewModel.Closing(object obj)
        {
            throw new NotImplementedException();
        }
    }
}
