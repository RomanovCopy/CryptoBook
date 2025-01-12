using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMenuEncryptionViewModel
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
        /// шифрованиефайлов и каталогов
        /// </summary>
        public ICommand Encrypt { get; }
        /// <summary>
        /// дешифрование файлов и каталогов
        /// </summary>
        public ICommand Decrypt { get; }

    }
}
