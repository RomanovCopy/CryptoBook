using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMainWindowViewModel:IViewModel
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
        /// открытие бокового меню
        /// </summary>
        public ICommand SideMenuOpen { get; }
        /// <summary>
        /// закрытие бокового меню
        /// </summary>
        public ICommand SideMenuClose { get; }

        /// <summary>
        /// добавление страницы
        /// </summary>
        public ICommand FrameListAddPage { get; }
        /// <summary>
        /// удаление страницы
        /// </summary>
        public ICommand FrameListRemovePage { get; }
        /// <summary>
        /// переход к следующей странице
        /// </summary>
        public ICommand FramelistGoForward { get; }
        /// <summary>
        /// переход к предыдущей странице
        /// </summary>
        public ICommand FramelistGoBack { get; }
        /// <summary>
        /// закрытие страницы
        /// </summary>
        public ICommand PageClosed { get; }

    }
}
