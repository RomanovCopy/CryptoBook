using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using CryptoBook.Infrastructure;

namespace CryptoBook.Interfaces
{
    public interface ITimeWebAIPageViewModel:IViewModel
    {
        /// <summary>
        /// команда обработки события окончания загрузки страницы
        /// </summary>
        public ICommand PageLoaded { get; }
        /// <summary>
        /// комманда обработки очистки страницы
        /// </summary>
        public ICommand PageClear { get; }
    }
}
