using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autofac;

using CryptoBook.Views;

namespace CryptoBook.Locators
{
    class MyWindows
    {
        public static MainWindow MainWindow => App.Container.Resolve<MainWindow>();
        public static ProgressWindow ProgressWindow => App.Container.Resolve<ProgressWindow>();


    }
}
