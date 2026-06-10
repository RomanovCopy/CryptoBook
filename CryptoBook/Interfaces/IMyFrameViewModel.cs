using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMyFrameViewModel: IViewModel
    {
        /// <summary>
        /// коллекция страниц
        /// </summary>
        public ObservableCollection<Page> FrameList { get; }
        /// <summary>
        /// текущая страница
        /// </summary>
        public Page CurrentPage { get; }



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


    }
}
