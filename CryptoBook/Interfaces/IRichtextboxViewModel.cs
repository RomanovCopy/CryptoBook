using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// ViewModel для управления RichTextBox с поддержкой форматирования текста и команд.
    /// </summary>
    public interface IRichtextboxViewModel:IViewModel
    {
        /// <summary>
        /// Документ RichTextBox.
        /// </summary>
        FlowDocument Document { get; set; }

        /// <summary>
        /// Признак жирного начертания текста.
        /// </summary>
        bool IsBold { get; set; }
        /// <summary>
        /// Признак курсивного начертания текста.
        /// </summary>
        bool IsItalic { get; set; }
        /// <summary>
        /// Признак подчёркнутого текста.
        /// </summary>
        bool IsUnderlined { get; set; }
        /// <summary>
        /// Размер шрифта.
        /// </summary>
        double FontSize { get; set; }


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
        /// Команда для вставки текста.
        /// </summary>
        ICommand InsertTextCommand { get; }
        /// <summary>
        /// Команда для очистки текста.
        /// </summary>
        ICommand ClearTextCommand { get; }
        /// <summary>
        /// Команда для вставки изображения.
        /// </summary>
        ICommand InsertImageCommand { get; }

        /// <summary>
        /// Команда для копирования текста.
        /// </summary>
        ICommand CopyCommand { get; }
        /// <summary>
        /// Команда для вырезания текста.
        /// </summary>
        ICommand CutCommand { get; }
        /// <summary>
        /// Команда для вставки текста из буфера обмена.
        /// </summary>
        ICommand PasteCommand { get; }

        /// <summary>
        /// Команда для изменения размера шрифта.
        /// </summary>
        ICommand ChangeFontSizeCommand { get; }

    }
}
}
