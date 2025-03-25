using Autofac;

using CryptoBook.Injections;
using CryptoBook.Views;

using System.Configuration;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace CryptoBook
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App: System.Windows.Application
    {

        public static IContainer Container { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var startup = new Startup();

            Container = startup.ConfigureServices(this);

            if(Container != null)
            {
                // Разрешение и запуск главного окна
                var mainWindow = Container?.Resolve<MainWindow>();
                mainWindow?.Show();
            } 

        }
    }

}
