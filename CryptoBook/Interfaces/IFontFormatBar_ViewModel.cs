using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IFontFormatBar_ViewModel: IViewModel
    {
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