using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

namespace CryptoBook.ViewModels
{
    public class MyMessageBox_ViewModel: ViewModelBase, IMyMessageBox_ViewModel
    {
        private readonly MyMessageBox_Model myMessageBox_Model;





        public MyMessageBox_ViewModel(ILifetimeScope scope)
        {
            myMessageBox_Model = new MyMessageBox_Model(scope);
            myMessageBox_Model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }





        public ICommand Loaded => loaded ??= new RelayCommand(myMessageBox_Model.Execute_Loaded, myMessageBox_Model.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Closed => closed ??= new RelayCommand(myMessageBox_Model.Execute_Closed, myMessageBox_Model.CanExecute_Closed);
        RelayCommand closed;

        public ICommand Closing => closing ??= new RelayCommand(myMessageBox_Model.Execute_Closing, myMessageBox_Model.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Close => throw new NotImplementedException();
    }
}
