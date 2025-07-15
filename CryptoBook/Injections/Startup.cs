using Autofac;

using CryptoBook.Converters;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.MyControls;
using CryptoBook.MyPages;
using CryptoBook.ViewModels;
using CryptoBook.Views;

namespace CryptoBook.Injections
{
    public class Startup
    {
        public IContainer ConfigureServices(System.Windows.Application app)
        {

            ContainerBuilder builder = new();

            //App
            builder.RegisterInstance(app).As<System.Windows.Application>().SingleInstance();


            //ViewModels
            builder.RegisterType<HomeViewModel>().As<IHomeViewModel>().SingleInstance();
            builder.RegisterType<TitleBarViewModel>().As<ITitleBarViewModel>().SingleInstance();
            builder.RegisterType<MyFrameViewModel>().As<IMyFrameViewModel>().SingleInstance();
            builder.RegisterType<MenuFileViewModel>().As<IMenuFileViewModel>().SingleInstance();
            builder.RegisterType<SideMenuViewModel>().As<ISideMenuViewModel>().SingleInstance();
            builder.RegisterType<MenuSettingsViewModel>().As<IMenuSettingsViewModel>().SingleInstance();
            builder.RegisterType<MenuEncryptionViewModel>().As<IMenuEncryptionViewModel>().SingleInstance();
            builder.RegisterType<MenuContentViewModel>().As<IMenuContentViewModel>().SingleInstance();
            builder.RegisterType<TitleBarRB_ViewModel>().As<ITitleBarRB_ViewModel>().SingleInstance();
            builder.RegisterType<RichtextboxViewModel>().As<IRichtextboxViewModel>().SingleInstance();

            //Converters
            builder.RegisterType<BitmapConverter>().AsSelf();
            builder.RegisterType<ColorToColorConverter>().InstancePerDependency();
            builder.RegisterType<ColumnsWidthConverter>().AsSelf();
            builder.RegisterType<SizeLocationConverter>().AsSelf();
            builder.RegisterType<FontSizeAdjustConverter>().AsSelf();
            builder.RegisterType<MediBrushSerializeConverter>().AsSelf();
            builder.RegisterType<VisibilityConverter>().AsSelf();
            builder.RegisterType<InternalSizeConverter>().AsSelf();


            //Helpers


            //Windows
            builder.RegisterType<MainWindowViewModel>().As<IMainWindowViewModel>().SingleInstance();
            builder.RegisterType<MainWindow>().SingleInstance();

            builder.RegisterType<ProgressViewModel>().As<IProgressViewModel>().InstancePerDependency();
            builder.RegisterType<ProgressWindow>().InstancePerDependency();

            builder.RegisterType<MyMessageBox_ViewModel>().As<IMyMessageBox_ViewModel>().InstancePerDependency();
            builder.RegisterType<MyMessageBox>().InstancePerDependency();

            //Services
            builder.RegisterType<WindowManager>().As<IWindowManager>().SingleInstance();
            builder.RegisterType<ThemeManager>().As<IThemeManager>().SingleInstance();


            //Pages
            builder.RegisterType<Home>().SingleInstance();

            //Controls
            builder.RegisterType<TitleBar>().SingleInstance();
            builder.RegisterType<MyFrame>().SingleInstance();
            builder.RegisterType<SideMenu>().SingleInstance();
            builder.RegisterType<TitleBarRB>().SingleInstance();
            builder.RegisterType<Richtextbox>().SingleInstance();




            var container = builder.Build();

            return container;
        }

    }
}
