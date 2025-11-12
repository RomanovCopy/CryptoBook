using CryptoBook.DTO;
using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface INewFileDialogViewModel:IViewModel,IWindowWithId
    {
        /// <summary>
        /// Коллекция доступных шаблонов файлов.
        /// </summary>
        public IReadOnlyList<IFileTemplate> Templates { get; }

        /// <summary>
        /// Получение или установка текущего выбранного шаблона файла.
        /// </summary>
        public IFileTemplate? SelectedTemplate { get; set; }

        /// <summary>
        /// Возвращает или задает путь к целевому каталогу, в котором будут храниться или извлекаться новые файлы.
        /// </summary>
        /// <remarks>
        /// Убедитесь, что указанный каталог существует и приложение имеет необходимые разрешения для доступа к нему.
        ///Если каталог не существует, может потребоваться дополнительная логика для его создания перед использованием.
        /// </remarks>
        public string TargetDirectory { get; set; }

        /// <summary>
        /// Получает или задает имя файла, включая его расширение.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Получает сообщение об ошибке, связанное с текущей операцией или состоянием.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Возвращает или задает значение, указывающее, должен ли созданный файл быть помечен как доступный только для чтения.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Возвращает или задает значение, указывающее, следует ли скрывать созданный файл.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Возвращает или задает значение, указывающее, следует ли создавать файл с разрешением на запись.
        /// </summary>
        public bool CanWrite { get; set; }

        /// <summary>
        /// Возвращает или задает значение, определяющее необходимость отображения скрытых файлов в пользовательском интерфейсе.
        /// </summary>
        public bool ShowHiddenFiles { get; set; }

        /// <summary>
        /// Возвращает или задает значение, указывающее, должен ли целевой каталог создаваться автоматически, если он отсутствует.
        /// </summary>
        public bool CreateDirectoryIfMissing { get; set; }

        /// <summary>
        /// Возвращает или задает поведение, применяемое, если файл с таким именем уже существует.
        /// </summary>
        /// <remarks>
        /// Значение определяет необходимость перезаписи, сбоя или автоматического переименования.
        /// </remarks>
        public IfExistsMode IfExists { get; set; }

        /// <summary>
        /// Команда открытия браузера папок для выбора директории.
        /// </summary>
        public ICommand Browse { get; }

        /// <summary>
        /// Команда для создания директории, если она еще не существует.
        /// </summary>
        public ICommand CreateDirectory { get; }

        /// <summary>
        /// Команда для инициализации предлагаемых значений для FileName и SelectedTemplate
        /// </summary>
        public ICommand InitSuggested { get; }

        /// <summary>
        /// Команда для создания файла с использованием текущих параметров.
        /// </summary>
        public ICommand Create { get; }

        /// <summary>
        /// Команда для отмены диалогового окна и отмены изменений.
        /// </summary>
        public ICommand Cancel { get; }
    }
}
