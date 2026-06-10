using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    /// <summary>
    /// варианты действий, если файл существует
    /// </summary>
    public enum IfExistsMode
    {
        /// <summary>
        /// выброс ошибки
        /// </summary>
        FailIfExists,
        /// <summary>
        /// перезаписать
        /// </summary>
        Overwrite,
        /// <summary>
        /// автоматическое переименование
        /// </summary>
        AutoRename // "MyFile (2).txt"
    }
}
