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
    public class MyMessageBox_ViewModel: ViewModelBase, IMyMessageBox_ViewModel, IWindowWithId
    {
        private readonly MyMessageBox_Model myMessageBox_Model;
        public Guid WindowId { get => myMessageBox_Model.WindowId; }


        public double WindowTop { get => myMessageBox_Model.WindowTop; set => myMessageBox_Model.WindowTop = value; }
        public double WindowLeft { get => myMessageBox_Model.WindowLeft; set => myMessageBox_Model.WindowLeft = value; }
        public string Header { get => myMessageBox_Model.Header; set => myMessageBox_Model.Header = value; }
        public string Message { get => myMessageBox_Model.Message; set => myMessageBox_Model.Message = value; }
        public string ButtonLeft_Content { get => myMessageBox_Model.ButtonLeft_Content; 
            set => myMessageBox_Model.ButtonLeft_Content = value; }
        public string ButtonRight_Content { get => myMessageBox_Model.ButtonRight_Content;
            set => myMessageBox_Model.ButtonRight_Content = value; }
        public Color BackColor { get => myMessageBox_Model.BackColor; set => myMessageBox_Model.BackColor = value; }
        public Color TextColor { get => myMessageBox_Model.TextColor; set => myMessageBox_Model.TextColor = value; }
        public Color ButtonLeftBC { get => myMessageBox_Model.ButtonLeftBC; set => myMessageBox_Model.ButtonLeftBC = value; }
        public Color ButtonLeftFC { get => myMessageBox_Model.ButtonLeftFC; set => myMessageBox_Model.ButtonLeftFC = value; }
        public Color ButtonRightBC { get => myMessageBox_Model.ButtonRightBC; set => myMessageBox_Model.ButtonRightBC = value; }
        public Color ButtonRightFC { get => myMessageBox_Model.ButtonRightFC; set => myMessageBox_Model.ButtonRightFC = value; }



        public MyMessageBox_ViewModel(ILifetimeScope scope)
        {
            myMessageBox_Model = new MyMessageBox_Model(scope);
            myMessageBox_Model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }



        public ICommand ButtonLeftClick => buttonLeftClick ??= new RelayCommand(myMessageBox_Model.Execute_ButtonLeftClick, myMessageBox_Model.CanExecute_ButtonLeftClick);
        RelayCommand buttonLeftClick;
        public ICommand ButtonRightClick => buttonRightClick ??= new RelayCommand(myMessageBox_Model.Execute_ButtonRightClick, myMessageBox_Model.CanExecute_ButtonRightClick);
        RelayCommand buttonRightClick;



        public ICommand Loaded => loaded ??= new RelayCommand(myMessageBox_Model.Execute_Loaded, myMessageBox_Model.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Closed => closed ??= new RelayCommand(myMessageBox_Model.Execute_Closed, myMessageBox_Model.CanExecute_Closed);
        RelayCommand closed;

        public ICommand Closing => closing ??= new RelayCommand(myMessageBox_Model.Execute_Closing, myMessageBox_Model.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Close => throw new NotImplementedException();

    }
}
