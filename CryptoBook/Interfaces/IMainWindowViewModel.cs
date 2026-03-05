using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMainWindowViewModel: IViewModel,IWindowOptions, IWindowWithId
    {
        /// <summary>
        /// скрыть окно в трее
        /// </summary>
        public ICommand WindowToMinimize { get; }
        /// <summary>
        /// окно в полный экран
        /// </summary>
        public ICommand WindowToMaximize { get; }
        /// <summary>
        /// оконный режим
        /// </summary>
        public ICommand WindowToNormal { get; }
        /// <summary>
        /// открытие/закрытие боковой панели от кнопки
        /// </summary>
        public ICommand ToggleMenuCommand { get; }
        /// <summary>
        /// закрытие бокового меню при клике вне его площади
        /// </summary>
        public ICommand SideMenuClose { get; }

    }
}
