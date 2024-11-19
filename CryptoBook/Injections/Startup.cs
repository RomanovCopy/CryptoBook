using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using System.Windows;
using CryptoBook.Views;
using CryptoBook.Converters;
using CryptoBook.Infrastructure;
using CryptoBook.ViewModels;
using CryptoBook.Interfaces;
using CryptoBook.MyPages;
using CryptoBook.MyControls;
using CryptoBook.Helpers;

namespace CryptoBook.Injections
{
    public class Startup
    {
        public static IContainer ConfigureServices()
        {

            ContainerBuilder builder = new ();


            //Windows
            builder.RegisterType<MainWindow>().SingleInstance();


            //Pages
            builder.RegisterType<Home>().SingleInstance();


            //Controls
            builder.RegisterType<SideMenu>().SingleInstance();


            //ViewModels
            builder.RegisterType<MainWindowViewModel>().As<IMainWindowViewModel>().SingleInstance();
            builder.RegisterType<HomeViewModel>().As<IHomeViewModel>().SingleInstance();
            builder.RegisterType<SideMenuViewModel>().As<ISideMenuViewModel>().SingleInstance();


            //Converters
            builder.RegisterType<BitmapConverter>().AsSelf();
            builder.RegisterType<ColorToColorConverter>().AsSelf();
            builder.RegisterType<ColumnsWidthConverter>().AsSelf();
            builder.RegisterType<SizeLocationConverter>().AsSelf();


            //Helpers
            builder.RegisterType<Languages>().SingleInstance();

            builder.RegisterType<BehaviorComboBox>().SingleInstance();




            return builder.Build();
        }
    }
}
