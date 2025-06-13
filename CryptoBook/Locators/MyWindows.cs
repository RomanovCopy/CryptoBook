using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autofac;

using CryptoBook.Views;
using CryptoBook.Interfaces;

namespace CryptoBook.Locators
{
    class MyWindows
    {
        public static MainWindow MainWindow => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<MainWindow>();
        public static ProgressWindow ProgressWindow => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<ProgressWindow>();


    }
}
