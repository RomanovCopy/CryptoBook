using Autofac;

using CryptoBook.Injections;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using WpfMessageBox = System.Windows.MessageBox;

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
            LocalizationManager.InitializeFromSettings();

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException +=
                OnUnhandledException;
            TaskScheduler.UnobservedTaskException +=
                OnUnobservedTaskException;

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

                _container.Resolve<IThemeManager>().Initialize();
                var windowManager = _container.Resolve<IWindowManager>();

                windowManager.ShowWindow(windowManager.CreateWindow<MainWindow>());

            } catch(Exception exception)
            {
                WriteCrashLog(exception, "Startup");
                WpfMessageBox.Show(
                    LocalizationManager.GetString("App.StartupFailureMessage"),
                    LocalizationManager.GetString("App.StartupFailureTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _container?.Dispose();
            } finally
            {
                base.OnExit(e);
            }
        }

        private static void OnDispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs args)
        {
            WriteCrashLog(args.Exception, "Dispatcher");
        }

        private static void OnUnhandledException(
            object? sender,
            UnhandledExceptionEventArgs args)
        {
            if(args.ExceptionObject is Exception exception)
                WriteCrashLog(exception, "AppDomain");
        }

        private static void OnUnobservedTaskException(
            object? sender,
            UnobservedTaskExceptionEventArgs args)
        {
            WriteCrashLog(args.Exception, "TaskScheduler");
        }

        private static void WriteCrashLog(
            Exception exception,
            string source)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "CryptoBook",
                    "Logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(
                    directory,
                    $"crash-{DateTime.UtcNow:yyyyMMdd}.log");
                string entry =
                    $"[{DateTimeOffset.UtcNow:O}] {source}" +
                    Environment.NewLine +
                    exception +
                    Environment.NewLine +
                    new string('-', 72) +
                    Environment.NewLine;
                File.AppendAllText(path, entry, Encoding.UTF8);
            }
            catch
            {
                // Сбой журналирования не должен заменить исходную ошибку.
            }
        }

    }

}
