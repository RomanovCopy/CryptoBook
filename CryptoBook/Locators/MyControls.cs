using Autofac;

using CryptoBook.MyControls;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Locators
{
    public class MyControls
    {

        public static TitleBar TitleBar => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<TitleBar>();
        public static MyFrame MyFrame => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<MyFrame>();
        public static SideMenu SideMenu => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<SideMenu>();

    }
}
