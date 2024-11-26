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
        /// Открытие/Закрытие бокового меню
        /// </summary>
        public ICommand ToggleMenuClick { get; }
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
