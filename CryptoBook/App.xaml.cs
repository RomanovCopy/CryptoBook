using Autofac;

using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System.Windows;

namespace CryptoBook
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App: System.Windows.Application, IContainerProvider
    {
        private IDriveManagerService _driveManagerService;
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
                    _driveManagerService = Container.Resolve<IDriveManagerService>();
                    _driveManagerService.StartMonitoring();
                    // Разрешение и запуск главного окна
                    var winmanager = Container.Resolve<IWindowManager>();
                    var id = winmanager.CreateWindow<MainWindow>();
                    winmanager.ShowWindow(id);
                }

            } catch
            {
                Shutdown();
            }


        }
        protected override void OnExit(ExitEventArgs e)
        {
            _driveManagerService.Dispose();
            base.OnExit(e);
        }
    }

}
