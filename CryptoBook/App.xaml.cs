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
    public partial class App: System.Windows.Application
    {
        private IDriveManagerService? _driveManagerService;
        IContainer? _container;

        public App()
        {
            var startup = new Startup();
            _container = startup.ConfigureServices(this);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                if(_container is null)
                    throw new InvalidOperationException("Container is null.");

                _driveManagerService = _container.Resolve<IDriveManagerService>();
                _driveManagerService.StartMonitoring();

                var windowManager = _container.Resolve<IWindowManager>();

                windowManager.ShowWindow(windowManager.CreateWindow<MainWindow>());

            } catch
            {
                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _driveManagerService?.Dispose();
                _container?.Dispose();
            } finally
            {
                base.OnExit(e);
            }
        }

    }

}
