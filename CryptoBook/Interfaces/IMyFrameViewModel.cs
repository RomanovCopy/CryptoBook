using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMyFrameViewModel: IViewModel
    {
        string? CurrentPageKey { get; }
        Page? CurrentPage { get; }

        /// <summary>
        /// добавление страницы
        /// </summary>
        public ICommand Navigate { get; }
        /// <summary>
        /// удаление страницы
        /// </summary>
        public ICommand RemovePage { get; }
        /// <summary>
        /// переход к следующей странице
        /// </summary>
        public ICommand GoForward { get; }
        /// <summary>
        /// переход к предыдущей странице
        /// </summary>
        public ICommand GoBack { get; }


    }
}
