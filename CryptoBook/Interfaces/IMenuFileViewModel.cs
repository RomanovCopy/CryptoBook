using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMenuFileViewModel
    {
        /// <summary>
        /// новый файл
        /// </summary>
        public ICommand NewFile { get; }
        /// <summary>
        /// открыть файл
        /// </summary>
        public ICommand OpenFile { get; }

        /// <summary>
        /// сохранить файл
        /// </summary>
        public ICommand SaveFile { get; }
        /// <summary>
        /// сохранить файл как
        /// </summary>
        public ICommand SaveAsFile { get; }
        /// <summary>
        /// обзор файлов
        /// </summary>
        public ICommand FileOverview { get; }
        /// <summary>
        /// открыть директорию
        /// </summary>
        public ICommand OpenDirectory { get; }
        /// <summary>
        /// обновить/перезагрузить файл
        /// </summary>
        public ICommand UpdateFile { get; }
        /// <summary>
        /// закрыть файл
        /// </summary>
        public ICommand CloseFile { get; }
        /// <summary>
        /// синхронизация рабочего каталога со сторонним
        /// </summary>
        public ICommand WorkingDirectorySynchronization { get; }
    }
}
