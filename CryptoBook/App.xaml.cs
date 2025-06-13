using Autofac;

using CryptoBook.Injections;
using CryptoBook.Interfaces;
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
    public partial class App: System.Windows.Application, IContainerProvider
    {

        public IContainer Container { get; private set; }
        public App()
        {
            var startup = new Startup();
            Container = startup.ConfigureServices(this);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                if(Container != null)
                {
                    // Разрешение и запуск главного окна
                    var mainWindow = Container.Resolve<MainWindow>();
                    mainWindow?.Show();
                }

            } catch(Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Failed to start application: {ex.Message}");
                Shutdown();
            }


        }
    }

}
