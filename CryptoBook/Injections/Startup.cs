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
using CryptoBook.MyControls;

namespace CryptoBook.Injections
{
    public class Startup
    {
        public static IContainer ConfigureServices()
        {

            var builder = new ContainerBuilder();

            //Windows
            builder.RegisterType<MainWindow>().SingleInstance();

            //Pages
            builder.RegisterType<Home>().SingleInstance();


            //ViewModels
            builder.RegisterType<MainWindowViewModel>().As<IMainWindowViewModel>().SingleInstance();
            builder.RegisterType<HomeViewModel>().As<IHomeViewModel>().SingleInstance();


            //Converters
            builder.RegisterType<BitmapConverter>().AsSelf();
            builder.RegisterType<ColorToColorConverter>().AsSelf();
            builder.RegisterType<ColumnsWidthConverter>().AsSelf();
            builder.RegisterType<SizeLocationConverter>().AsSelf();


            //Helpers
            builder.RegisterType<Languages>().AsSelf();


            return builder.Build();
        }
    }
}
