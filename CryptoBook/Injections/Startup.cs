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


            //ViewModels
            builder.RegisterType<MainWindowViewModel>().As<IMainWindowViewModel>().SingleInstance();

            //Converters
            builder.RegisterType<BitmapConverter>().AsSelf();
            builder.RegisterType<ColorToColorConverter>().AsSelf();
            builder.RegisterType<ColumnsWidthConverter>().AsSelf();

            builder.RegisterType<Languages>().AsSelf();


            return builder.Build();
        }
    }
}
