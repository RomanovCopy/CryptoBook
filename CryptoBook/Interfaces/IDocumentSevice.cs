using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDocumentSevice
    {
        IRichTextBoxService service { get; }
        /// <summary>
        /// Сохранить документ в файл.
        /// </summary>
        /// <param name="filePath">Путь к файлу для сохранения.</param>
        void SaveDocument(string filePath);
        /// <summary>
        /// Загрузить документ из файла.
        /// </summary>
        /// <param name="filePath">Путь к файлу для загрузки.</param>
        void LoadDocument(string filePath);
        /// <summary>
        /// Очистить текущий документ.
        /// </summary>
        void ClearDocument();
    }
}
