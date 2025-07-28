using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IFontFormatBar_ViewModel: IViewModel
    {

        // Свойства
        bool IsBold { get; }
        bool IsItalic { get; }
        bool IsUnderline { get; }
        double FontSize { get; set; }
        string FontFamily { get; }
        string FontColor { get; }
        string FontStile { get; }

        /// <summary>
        /// Коллекция доступных размеров шрифта
        /// </summary>
        ObservableCollection<double> FontSizes { get; }
        /// <summary>
        /// Коллекция доступных семейств шрифтов
        /// </summary>
        ObservableCollection<string> FontFamilies { get; }
        /// <summary>
        /// Коллекция доступных цветов шрифта
        /// </summary>
        ObservableCollection<Color> FontColors { get; }
        /// <summary>
        /// Коллекция доступных цветов фона
        /// </summary>
        ObservableCollection<Brush> BackgrondColor { get; }



        /// <summary>
        /// Команда для применения жирного начертания.
        /// </summary>
        ICommand BoldCommand { get; }
        /// <summary>
        /// Команда для применения курсивного начертания.
        /// </summary>
        ICommand ItalicCommand { get; }
        /// <summary>
        /// Команда для применения подчёркивания.
        /// </summary>
        ICommand UnderlineCommand { get; }
        /// <summary>
        /// Команда для очистки форматирования.
        /// </summary>
        ICommand ClearFormattingCommand { get; }

        /// <summary>
        /// Команда для изменения размера шрифта.
        /// </summary>
        ICommand FontSizeCommand { get; }
        /// <summary>
        /// Команда для изменения семейства шрифта.
        /// </summary>
        ICommand FontFamilyCommand { get; }
        /// <summary>
        /// Команда для изменения выравнивания текста.
        /// </summary>
        ICommand TextAlligmentCommand { get; }
        /// <summary>
        /// Команда для изменения цвета текста (цвет переднего плана).
        /// </summary>
        ICommand ForegroundCommand { get; }
        /// <summary>
        /// Команда для изменения цвета фона текста.
        /// </summary>
        ICommand BackgroundCommand { get; }
    }
}