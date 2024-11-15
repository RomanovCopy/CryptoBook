using Autofac;

using CryptoBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Locators
{
    public class ViewModels
    {

        public MainWindowViewModel MainWindowViewModel=>App.Container.Resolve<>

    }
}
