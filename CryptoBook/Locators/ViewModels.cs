using Autofac;

using CryptoBook.Interfaces;
using CryptoBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WinRT;

namespace CryptoBook.Locators
{
    public class ViewModels
    {

        public MainWindowViewModel MainWindowViewModel => App.Container.Resolve<IMainWindowViewModel>().As<MainWindowViewModel>();

    }
}
