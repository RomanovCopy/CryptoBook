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
        /// открытие боковой панели
        /// </summary>
        public ICommand SideMenuOpen { get; }
        /// <summary>
        /// закрытие боковой панели
        /// </summary>
        public ICommand SideMenuClose { get; }
        /// <summary>
        /// обработка клика по окну
        /// </summary>
        public ICommand WindowPreviewMouseDown { get; }
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

    }
}
