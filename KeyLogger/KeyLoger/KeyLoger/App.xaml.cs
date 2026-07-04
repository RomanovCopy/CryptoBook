using Autofac;

using KeyLogger;
using KeyLogger.Interfaces;
using KeyLogger.Services;


using System.Configuration;
using System.Data;
using System.Windows;

namespace KeyLogger
{
    public partial class App: System.Windows.Application
    {
        private IContainer _container;

        public App()
        {
            // 1️⃣ Создаём контейнер
            var builder = new ContainerBuilder();

            // 2️⃣ Регистрация сервисов и вьюмоделей
            builder.RegisterType<KeyboardHookService>().As<IKeyboardHookService>().SingleInstance();          // один экземпляр на всё приложение

            builder.RegisterType<KeyLogger.ViewModels.MainViewModel>();

            // 3️⃣ (Опционально) регистрируем MainWindow, чтобы можно было разрешить его тоже
            builder.RegisterType<MainWindow>()
                   .PropertiesAutowired()        // автоматическое привязывание свойств (если понадобится)
                   .SingleInstance();           // окно как singleton

            _container = builder.Build();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 4️⃣ Получаем окно из контейнера – Autofac сам разрешит его зависимостям
            var mainWindow = _container?.Resolve<MainWindow>();

            // 5️⃣ Устанавливаем DataContext вручную, т.к. MainWindow не знает о ViewModel
            mainWindow.DataContext = _container.Resolve<KeyLogger.ViewModels.MainViewModel>();

            // 6️⃣ Показываем окно и запускаем хук
            mainWindow.Show();

            var hookService = _container.Resolve<IKeyboardHookService>();
            hookService.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            var hookService = _container?.Resolve<IKeyboardHookService>();
            hookService?.Stop();

            _container?.Dispose();
        }
    }
}

