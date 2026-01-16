using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    /// <summary>
    ///Представляет элемент файла в файловой системе, предоставляя свойства и методы для операций, специфичных для файла, таких как
    ///переименование, удаление и перемещение файла.
    /// </summary>
    /// <remarks>Этот интерфейс расширяет IFileSystemItem и включает функции, специфичные для файла.
    ///Реализации могут представлять файлы на диске, в памяти или в виртуализированных файловых системах. Безопасность и
    ///поддержка операций может варьироваться в зависимости от реализации.</remarks>
    public interface IFileItem:IFileSystemItem
    {
        long? Size { get; set; }
        string Extension { get; set; }

    }
}
