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

        public static MenuFileViewModel MenuFileViewModel =>
            App.Container.Resolve<IMenuFileViewModel>().As<MenuFileViewModel>();

        public static MenuSettingsViewModel MenuSettingsViewModel =>
            App.Container.Resolve<IMenuSettingsViewModel>().As<MenuSettingsViewModel>();

        public static MenuEncryptionViewModel MenuEncryptionViewModel =>
            App.Container.Resolve<IMenuEncryptionViewModel>().As<MenuEncryptionViewModel>();

        public static MenuContentViewModel MenuContentViewModel =>
            App.Container.Resolve<IMenuContentViewModel>().As<MenuContentViewModel>();

        public static ProgressViewModel ProgressViewModel =>
            App.Container.Resolve<IProgressViewModel>().As<ProgressViewModel>();


    }
}
