using Autofac;

using CryptoBook.Injections;
using CryptoBook.Views;

using System.Configuration;
using System.Data;
using System.Windows;

namespace CryptoBook
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App: System.Windows.Application
    {
        public static IContainer? Container { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Container = (IContainer)Injections.Startup.ConfigureServices();

            // Разрешение и запуск главного окна
            var mainWindow = Container?.Resolve<MainWindow>();
            mainWindow?.Show();
        }
    }

}
