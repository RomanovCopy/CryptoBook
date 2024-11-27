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

        public static MainWindowViewModel MainWindowViewModel =>
            App.Container.Resolve<IMainWindowViewModel>().As<MainWindowViewModel>();

        public static HomeViewModel HomeViewModel => 
            App.Container.Resolve<IHomeViewModel>().As<HomeViewModel>();

        public static SideMenuViewModel SideMenuViewModel =>
            App.Container.Resolve<ISideMenuViewModel>().As<SideMenuViewModel>();

        public static TitleBarViewModel TitleBarViewModel =>
            App.Container.Resolve<ITitleBarViewModel>().As<TitleBarViewModel>();

        public static MyFrameViewModel MyFrameViewModel =>
            App.Container.Resolve<IMyFrameViewModel>().As<MyFrameViewModel>();
    }
}
