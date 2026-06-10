using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Models
{
    public class SystemItemName_Editor_Model: ViewModelBase, ISystemItemName_Editor_Model
    {

        private readonly IWindowManager windowManager;

        private IWindowContext windowContext;
        public Guid WindowId { get => windowId; private set => windowId = value; }
        Guid windowId;

        public double WindowWidth { get => windowWidth; set => SetProperty(ref windowWidth, value); }
        double windowWidth;
        public double WindowHeight { get => windowHeight; set => SetProperty(ref windowHeight, value); }
        double windowHeight;
        public double WindowTop { get => windowTop; set => SetProperty(ref windowTop, value); }
        double windowTop;
        public double WindowLeft { get => windowLeft; set => SetProperty(ref windowLeft, value); }
        double windowLeft;
        public WindowState WindowState { get => windowState; set => SetProperty(ref windowState, value); }
        WindowState windowState;

        public string OldName { get => oldName; private set => SetProperty(ref oldName, value); }
        string oldName;
        public string OldExtension { get => oldExtension; private set => SetProperty(ref oldExtension, value); }
        string oldExtension;
        public string NewName { get => newName; set => SetProperty(ref newName, value); }
        string newName;
        public string NewExtension { get => newExtension; set => SetProperty(ref newExtension, value); }
        string newExtension;



        public SystemItemName_Editor_Model(IWindowManager windowManager, IWindowContext windowContext)
        {
            WindowId = Guid.NewGuid();
            this.windowManager = windowManager??throw new ArgumentNullException(nameof(windowManager));
            if(windowContext is IWindowContext context && context.Items["SystemItem"] is ISystemItem systemItem)
            {
                this.windowContext = context;
                OldName = systemItem.Name;
                NewName = systemItem.Name;
                OldExtension = Path.GetExtension(systemItem.Name);
                NewExtension = Path.GetExtension(systemItem.Name);
            }

        }



        public bool CanExecute_SaveCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_SaveCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_RestoreCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_RestoreCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        public void Execute_Loaded(object? obj)
        {
        }
        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }

        public bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }


    }
}
