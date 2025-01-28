using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Models
{
    internal class MyMessageBox_Model:ViewModelBase
    {
        private readonly ILifetimeScope scope;
        private readonly IWindowManager windowManager;

        internal string Header { get => header; set => SetProperty(ref header, value); }
        private string header;

        internal string Message { get => message; set => SetProperty(ref message, value); }

        internal Brush BackColor { get => backColor; set => SetProperty(ref backColor, value); }
        Brush backColor;
        internal Brush TextColor { get => textColor, set => SetProperty(ref textColor, value); }
        Brush textColor;


        public double WindowTop { get=> windowTop; set=>SetProperty(ref windowTop, value); }
        double windowTop;
        public double WindowLeft { get=> windowLeft; set=>SetProperty(ref windowLeft, value); }
        double windowLeft;

        private string message;

        public MyMessageBox_Model(ILifetimeScope scope)
        {
            this.scope = scope;
            windowManager = scope.Resolve<IWindowManager>();
            
        }



        internal bool CanExecute_SetMessage(object? obj)
        {
            return obj is string;
        }
        internal void Execute_SetMessage(object? obj)
        {
            if(obj is string str)
            {
                Message = str;
            }
        }


        internal bool CanExecute_SetHeader(object? obj)
        {
            return obj is string;
        }
        internal void Execute_SetHeader(object? obj)
        {
            if(obj is string str)
            {
                Header = str;
            }
        }



        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
            WindowLeft = Properties.Settings.Default.MyMessageLeft;
            WindowTop = Properties.Settings.Default.MyMessageTop;
        }


        internal bool CanExecute_Closed(object? obj)
        {
            return true;
        }

        internal void Execute_Closed(object? obj)
        {

        }


        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
        {
            Properties.Settings.Default.MyMessageLeft= WindowLeft;
            Properties.Settings.Default.MyMessageTop= WindowTop;
            Properties.Settings.Default.Save();
        }

    }
}
