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
            ((IContainerProvider)App.Current).Container.Resolve<IMainWindowViewModel>().As<MainWindowViewModel>();

        public static HomeViewModel HomeViewModel => 
            ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<IHomeViewModel>().As<HomeViewModel>();

        public static SideMenuViewModel SideMenuViewModel =>
            ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<ISideMenuViewModel>().As<SideMenuViewModel>();

        public static TitleBarViewModel TitleBarViewModel =>
            ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<ITitleBarViewModel>().As<TitleBarViewModel>();

        public static MyFrameViewModel MyFrameViewModel =>
            ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<IMyFrameViewModel>().As<MyFrameViewModel>();

        public static MenuFileViewModel MenuFileViewModel =>
            ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<IMenuFileViewModel>().As<MenuFileViewModel>();

        public static MenuSettingsViewModel MenuSettingsViewModel =>
            ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<IMenuSettingsViewModel>().As<MenuSettingsViewModel>();

        public static MenuEncryptionViewModel MenuEncryptionViewModel =>
            ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<IMenuEncryptionViewModel>().As<MenuEncryptionViewModel>();

        public static MenuContentViewModel MenuContentViewModel =>
            ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<IMenuContentViewModel>().As<MenuContentViewModel>();

        //public static ProgressViewModel ProgressViewModel =>
        //    ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<IProgressViewModel>().As<ProgressViewModel>();

        public static MyMessageBox_ViewModel MyMessageBoxViewModel => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<IMyMessageBox_ViewModel>().As<MyMessageBox_ViewModel>();


        public static ProgressViewModel ProgressViewModel =>
            ((IContainerProvider)System.Windows.Application.Current).Container.BeginLifetimeScope().Resolve<IProgressViewModel>().As<ProgressViewModel>();
    }
}
