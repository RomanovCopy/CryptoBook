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
        public static SideMenu SideMenu => App.Container.Resolve<SideMenu>();
        public static TitleBar TitleBar => App.Container.Resolve<TitleBar>();
        public static MyFrame MyFrame => App.Container.Resolve<MyFrame>();

    }
}
