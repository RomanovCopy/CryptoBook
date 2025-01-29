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
    public class MyMessageBox_ViewModel: ViewModelBase, IMyMessageBox_ViewModel,IWindowWithId
    {
        private readonly MyMessageBox_Model myMessageBox_Model;

        public double WindowTop { get => myMessageBox_Model.WindowTop; set => myMessageBox_Model.WindowTop = value; }
        public double WindowLeft { get => myMessageBox_Model.WindowLeft; set => myMessageBox_Model.WindowLeft = value; }
        public Brush BackColor { get => myMessageBox_Model.BackColor; set => myMessageBox_Model.BackColor = value; }

        public Brush TextColor { get => myMessageBox_Model.TextColor; set => myMessageBox_Model.TextColor = value; }


        public string Header => myMessageBox_Model.Header;

        public string Message => myMessageBox_Model.Message;

        public Guid WindowId => myMessageBox_Model.WindowId;


        public MyMessageBox_ViewModel(ILifetimeScope scope)
        {
            myMessageBox_Model = new MyMessageBox_Model(scope);
            myMessageBox_Model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand SetHeader => setHeader ??= new RelayCommand(myMessageBox_Model.Execute_SetHeader, myMessageBox_Model.CanExecute_SetHeader);
        RelayCommand setHeader;

        public ICommand SetMessage => setMessage ??= new RelayCommand(myMessageBox_Model.Execute_SetMessage, myMessageBox_Model.CanExecute_SetMessage);
        RelayCommand setMessage;





        public ICommand Loaded => loaded ??= new RelayCommand(myMessageBox_Model.Execute_Loaded, myMessageBox_Model.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Closed => closed ??= new RelayCommand(myMessageBox_Model.Execute_Closed, myMessageBox_Model.CanExecute_Closed);
        RelayCommand closed;

        public ICommand Closing => closing ??= new RelayCommand(myMessageBox_Model.Execute_Closing, myMessageBox_Model.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Close => throw new NotImplementedException();

    }
}
