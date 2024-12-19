using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMenuEncryptionViewModel:IViewModel
    {
        /// <summary>
        /// удаление ключа шифрования
        /// </summary>
        public ICommand DeleteKey { get; }
        /// <summary>
        /// установка ключа шифрования
        /// </summary>
        public ICommand InstalKey { get; }
        /// <summary>
        /// шифрование/дешифрование файлов и каталогов
        /// </summary>
        public ICommand EncryptionPanel { get; }
    }
}
