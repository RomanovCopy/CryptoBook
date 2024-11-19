using Autofac;

using CryptoBook.MyControls;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Locators
{
    public class MyControls
    {
        public SideMenu SideMenu => App.Container.Resolve<SideMenu>();

    }
}
