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
    public interface IRichtextboxViewModel: IViewModel
    {
        /// <summary>
        /// Документ RichTextBox.
        /// </summary>
        FlowDocument Document { get;}

        /// <summary>
        /// Признак жирного начертания текста.
        /// </summary>
        bool IsBold { get; }
        /// <summary>
        /// Признак курсивного начертания текста.
        /// </summary>
        bool IsItalic { get;}
        /// <summary>
        /// Признак подчёркнутого текста.
        /// </summary>
        bool IsUnderlined { get; }
        /// <summary>
        /// Размер шрифта.
        /// </summary>
        double FontSize { get;}
        /// <summary>
        /// семейство шрифтов.
        /// </summary>
        string FontFamily { get; }


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
        /// <summary>
        /// Команда для изменения FontFamily.
        /// </summary>
        ICommand ChangeFontFamilyCommand { get; }
        /// <summary>
        /// Команда для изменения цвета шрифта
        /// </summary>
        ICommand ChangeForegroundColor { get; }
        /// <summary>
        /// Команда для изменения цвета фона(бумаги).
        /// </summary>
        ICommand ChangeBackgroundColor { get; }
        /// <summary>
        /// Устанавливает выравнивание текста (Left, Center, Right, Justify)
        /// </summary>
        ICommand ApplyTextAlignment { get; }
        /// <summary>
        /// Разрешение табуляции
        /// </summary>
        ICommand ApplyAcceptsTab { get; }
        /// <summary>
        /// Разрешение возврата каретки (Enter)
        /// </summary>
        ICommand ApplyAcceptsReturn { get; }
        /// <summary>
        /// Установка видимости вертикальной полосы прокрутки
        /// </summary>
        ICommand ApplyVerticalScrollBarVisibility { get; }
        /// <summary>
        /// Установка видимости горизонтальной полосы прокрутки
        /// </summary>
        ICommand ApplyHorizontalScrollBarVisibility { get; }
        /// <summary>
        /// Установка контекстного меню
        /// </summary>
        ICommand ApplyContextMenu { get; }
        /// <summary>
        /// Сбрасывает форматирование выделенного текста
        /// </summary>
        ICommand ClearFormatting { get; }
        /// <summary>
        /// Выделяет весь текст в документе
        /// </summary>
        ICommand SelectAll { get; }
        /// <summary>
        /// Снимает выделение текста
        /// </summary>
        ICommand ClearSelection { get; }
        /// <summary>
        /// Возвращает выделенный текст в виде строки
        /// </summary>
        ICommand GetSelectedTextAsString { get; }
        /// <summary>
        /// Заменяет выделенный текст на указанный
        /// </summary>
        ICommand ReplaceSelectedText { get; }
        /// <summary>
        /// Вставляет гиперссылку с указанным URI и необязательным отображаемым текстом
        /// </summary>
        ICommand InsertHyperlink { get; }
        /// <summary>
        /// Вставляет новый параграф в текущей позиции курсора
        /// </summary>
        ICommand InsertParagraph { get; }
        /// <summary>
        /// Вставляет разрыв строки
        /// </summary>
        ICommand InsertLineBreak { get; }
        /// <summary>
        /// Вставляет таблицу с указанным количеством строк и столбцов
        /// </summary>
        ICommand InsertTable { get; }
        /// <summary>
        /// Возвращает содержимое документа в формате RTF
        /// </summary>
        ICommand GetRtf { get; }
        /// <summary>
        /// Загружает содержимое документа из строки RTF
        /// </summary>
        ICommand LoadRtf { get; }
        /// <summary>
        /// Загружает обычный текст в документ
        /// </summary>
        ICommand LoadPlainText { get; }
        /// <summary>
        /// Очищает содержимое документа
        /// </summary>
        ICommand ClearDocument { get; }
        /// <summary>
        /// Прокручивает документ к позиции курсора
        /// </summary>
        ICommand ScrollToCaret { get; }
        /// <summary>
        /// Прокручивает документ в конец
        /// </summary>
        ICommand ScrollToEnd { get; }
        /// <summary>
        /// Прокручивает документ в начало
        /// </summary>
        ICommand ScrollToStart { get; }
        /// <summary>
        /// Устанавливает отступы для документа
        /// </summary>
        ICommand SetDocumentMargin { get; }
        /// <summary>
        /// Отменяет последнее действие
        /// </summary>
        ICommand Undo { get; }
        /// <summary>
        /// Повторяет отмененное действие
        /// </summary>
        ICommand Redo { get; }
        /// <summary>
        /// Ищет текст в документе
        /// </summary>
        ICommand FindText { get; }
        /// <summary>
        /// Заменяет первое вхождение текста
        /// </summary>
        ICommand ReplaceText { get; }
        /// <summary>
        /// Заменяет все вхождения текста
        /// </summary>
        ICommand ReplaceAllText { get; }
        /// <summary>
        /// Применяет маркированный список к выделенному тексту
        /// </summary>
        ICommand ApplyBulletedList { get; }
        /// <summary>
        /// Применяет нумерованный список к выделенному тексту
        /// </summary>
        ICommand ApplyNumberedList { get; }
        /// <summary>
        /// Удаляет форматирование списка
        /// </summary>
        ICommand RemoveListFormatting { get; }
        /// <summary>
        /// Увеличивает отступ для текущего параграфа
        /// </summary>
        ICommand IncreaseIndent { get; }
        /// <summary>
        /// Уменьшает отступ для текущего параграфа
        /// </summary>
        ICommand DecreaseIndent { get; }
        /// <summary>
        /// Устанавливает фокус на элементе управления
        /// </summary>
        ICommand Focus { get; }
        /// <summary>
        /// Вставляет текст в текущей позиции курсора
        /// </summary>
        ICommand InsertTextAtCaret { get; }
    }
}
