using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

using Autofac;


using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Models
{
    internal class MyMessageBox_Model:ViewModelBase
    {
        private readonly ILifetimeScope scope;
        private readonly IWindowManager windowManager;
        internal readonly Guid WindowId;

        internal string Header { get => header; set => SetProperty(ref header, value); }
        private string header;

        internal string Message { get => message; set => SetProperty(ref message, value); }
        private string message;

        public string ButtonLeft_Content { get=>buttonLeft_Content; set=>SetProperty(ref buttonLeft_Content, value); }
        private string buttonLeft_Content;
        public string ButtonRight_Content { get=>buttonRight_Content; set=>SetProperty(ref buttonRight_Content, value); }
        private string buttonRight_Content;

        internal Color BackColor { get => backColor; set => SetProperty(ref backColor, value); }
        private Color backColor;
        internal Color TextColor { get => textColor; set => SetProperty(ref textColor, value); }
        private Color textColor;


        public double WindowTop { get=> windowTop; set=>SetProperty(ref windowTop, value); }
        private double windowTop;
        public double WindowLeft { get=> windowLeft; set=>SetProperty(ref windowLeft, value); }
        private double windowLeft;
        public Color ButtonLeftBC { get=>buttonLeftBC; set=>SetProperty(ref buttonLeftBC, value); }
        private Color buttonLeftBC;
        public Color BullonLeftFC { get; internal set; }
        public Color ButtonRightBC { get; internal set; }
        public Color ButtonRightFC { get; internal set; }

        public MyMessageBox_Model(ILifetimeScope scope)
        {
            WindowId = Guid.NewGuid();
            this.scope = scope;
            windowManager = scope.Resolve<IWindowManager>();
            
        }

        internal bool CanExecute_ButtonLeftClick(object? obj)
        {
            return true;
        }
        internal void Execute_ButtonLeftClick(object? obj)
        {
            
        }

        internal bool CanExecute_ButtonRightClick(object? obj)
        {
            return true;
        }
        internal void Execute_ButtonRightClick(object? obj)
        {
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
