using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;
using CryptoBook.Views;

namespace CryptoBook.Models
{
    internal class MenuFileModel: ViewModelBase
    {
        private readonly IWindowManager windowManager;
        private readonly ILifetimeScope scope;


        internal MenuFileModel(ILifetimeScope scope)
        {
            this.scope = scope;
            windowManager = scope.Resolve<IWindowManager>();
        }

        internal bool CanExecute_NewFile(object obj)
        {
            return true;
        }
        internal void Execute_NewFile(object obj)
        {

        }

        internal bool CanExecute_OpenFile(object obj)
        {
            return true;
        }
        internal void Execute_OpenFile(object obj)
        {
            var window = windowManager.CreateWindow<ProgressWindow>();
            var vm = (ProgressViewModel)window.DataContext;
            vm.OperationName = "Open File";
            if(vm is IWindowWithId)
            {
                windowManager.ShowWindow<ProgressWindow>(vm.WindowId);
                vm.StartLongOperation.Execute(new TestLongOperarion());
            }
        }

        internal bool CanExecute_SaveFile(object obj)
        {
            return true;
        }
        internal void Execute_SaveFile(object obj)
        {

        }

        internal bool CanExecute_SaveAsFile(object obj)
        {
            return true;
        }
        internal void Execute_SaveAsFile(object obj)
        {
        }


        internal bool CanExecute_FileOverview(object obj)
        {
            return true;
        }
        internal void Execute_FileOverview(object obj)
        {
        }

        internal bool CanExecute_OpenDirectory(object obj)
        {
            return true;
        }

        internal void Execute_OpenDirectory(object obj)
        {
        }

        internal bool CanExecute_WorkingDirectorySynchronization(object obj)
        {
            return true;
        }

        internal void Execute_WorkingDirectorySynchronization(object obj)
        {
        }

        internal bool CanExecute_CloseFile(object obj)
        {
            return true;
        }
        internal void Execute_CloseFile(object obj)
        {
        }

        internal bool CanExecute_UpdateFile(object obj)
        {
            return true;
        }
        internal void Execute_UpdateFile(object obj)
        {
        }

    }
}
