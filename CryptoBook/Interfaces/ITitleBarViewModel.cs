using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface ITitleBarViewModel: IViewModel
    {
        /// <summary>
        /// двойной клик по TitleBar
        /// </summary>
        public ICommand TitleBarDoubleClick { get; }
        /// <summary>
        /// перемещение окна при зажатии MouseLeftButton над TitleBar
        /// </summary>
        public ICommand MouseLeftButtonDown { get; }
        /// <summary>
        /// событие. указатель мыши над TitleBar
        /// </summary>
        public ICommand TitleBarMouseMove { get; }
        /// <summary>
        /// переход к предыдущей странице
        /// </summary>
        public ICommand ButtonBack_Click { get; }
        /// <summary>
        /// переход к следующей странице
        /// </summary>
        public ICommand ButtonForward_Click { get; }
        /// <summary>
        /// открытие бокового меню
        /// </summary>
        public ICommand ToggleMenu_Click { get; }


        /// <summary>
        /// переключение на светлую тему
        /// </summary>
        public ICommand ButtonLightTheme_Click { get; }
        /// <summary>
        /// переключение на темную тему
        /// </summary>
        public ICommand ButtonDarkThemeClick { get; }
        /// <summary>
        /// вызов настроек
        /// </summary>
        public ICommand ButtonSettingsClick { get; }
        /// <summary>
        /// скрытие окна на панели задач
        /// </summary>
        public ICommand MinButtonClick { get; }
        /// <summary>
        /// перевод окна в оконный режим
        /// </summary>
        public ICommand GoToWindow { get; }
        /// <summary>
        /// переод окна в полноэкранный режим
        /// </summary>
        public ICommand MaxButtonClick { get; }
        /// <summary>
        /// закрытие окна
        /// </summary>
        public ICommand CloseButtonClick { get; }

    }
}
