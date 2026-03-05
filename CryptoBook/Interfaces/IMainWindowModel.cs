using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IMainWindowModel:IModel, IWindowOptions, IWindowWithId
    {
        /// <summary>
        /// скрыть окно в трее
        /// </summary>
        bool CanExecute_WindowToMinimize(object? obj);
        void Execute_WindowToMinimize(object? obj);
        /// <summary>
        /// окно в полный экран
        /// </summary>
        bool CanExecute_WindowToMaximize(object? obj);
        void Execute_WindowToMaximize(object? obj);
        /// <summary>
        /// оконный режим
        /// </summary>
        bool CanExecute_WindowToNormal(object? obj);
        void Execute_WindowToNormal(object? obj);
        /// <summary>
        /// открытие/закрытие боковой панели от кнопки
        /// </summary>
        bool CanExecute_ToggleMenuCommand(object? obj);
        void Execute_ToggleMenuCommand(object? obj);    
        /// <summary>
        /// закрытие бокового меню при клике вне его площади
        /// </summary>
        bool CanExecute_SideMenuClose(object? obj);     
        void Execute_SideMenuClose(object? obj);
    }
}
