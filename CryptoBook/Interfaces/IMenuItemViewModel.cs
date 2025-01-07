using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;


namespace CryptoBook.Interfaces
{
    public interface IMenuItemViewModel
    {
        /// <summary>
        /// выбор элемента
        /// </summary>
        public ICommand SelectItem { get; }

    }
}
