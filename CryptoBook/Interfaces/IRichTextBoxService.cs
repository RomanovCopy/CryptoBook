using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using Media = System.Windows.Media;
using Draving = System.Drawing;
using Controls = System.Windows.Controls;


namespace CryptoBook.Interfaces
{
    public interface IRichTextBoxService
    {
        // Свойства для работы с содержимым и выделением



        /// <summary>
        /// Получение/установка содержимого документа
        /// </summary>
        FlowDocument Document { get; set; }
        /// <summary>
        /// Получение текущего выделенного текста
        /// </summary>
        TextSelection Selection { get; }
        /// <summary>
        /// Проверяет, можно ли отменить действие
        /// </summary>
        bool CanUndo { get; }
        /// <summary>
        /// Проверяет, можно ли повторить действие
        /// </summary>
        bool CanRedo { get; }
        /// <summary>
        /// Позиция курсора в документе
        /// </summary>
        TextPointer CaretPosition { get; set; }
        /// <summary>
        /// Управление режимом "только чтение"
        /// </summary>
        bool IsReadOnly { get; set; }
        /// <summary>
        /// Включение/выключение проверки орфографии
        /// </summary>
        bool SpellCheckEnabled { get; set; }
        /// <summary>
        /// текущая FontFamily для вводимого текста
        /// </summary>
        Media.FontFamily FontFamily { get; set; }
        /// <summary>
        /// текущая FontWeight для вводимого текста
        /// </summary>
        FontWeight FontWeight { get; set; }
        /// <summary>
        /// текущая FontSize для вводимого текста
        /// </summary>
        double FontSize { get; set; }

        // Методы форматирования текста

        /// <summary>
        /// Применяет полужирное начертание к выделенному тексту
        /// </summary>
        void ApplyBold();
        /// <summary>
        /// Применяет курсив к выделенному тексту
        /// </summary>
        void ApplyItalic();
        /// <summary>
        /// Применяет подчеркивание к выделенному тексту
        /// </summary>
        void ApplyUnderline();
        /// <summary>
        /// Устанавливает размер шрифта для выделенного текста
        /// </summary>
        /// <param name="fontSize">размер шрифта</param>
        void ApplyFontSize(double fontSize);
        /// <summary>
        /// Устанавливает семейство шрифтов для выделенного текста
        /// </summary>
        /// <param name="fontFamily">семейство шрифтов</param>
        void ApplyFontFamily(string fontFamily);
        /// <summary>
        /// Устанавливает цвет текста для выделенного текста
        /// </summary>
        /// <param name="color">цвет текста</param>
        void ApplyForegroundColor(Media.Color color);
        /// <summary>
        /// Устанавливает цвет фона для выделенного текста
        /// </summary>
        /// <param name="color">цвет фона</param>
        void ApplyBackgroundColor(Media.Color color);
        /// <summary>
        /// Устанавливает выравнивание текста (Left, Center, Right, Justify)
        /// </summary>
        /// <param name="alignment">выравнивание текста</param>
        void ApplyTextAlignment(TextAlignment alignment);
        /// <summary>
        /// Установка форматирования текста
        /// </summary>
        /// <param name="format"></param>
        void ApplyTextFormattingMode(Media.TextFormattingMode mode);
        /// <summary>
        /// Установка режима рендеринга текста
        /// </summary>
        /// <param name="mode"></param>
        void ApplyTextRenderingMode(Media.TextRenderingMode mode);
        /// <summary>
        /// Разрешение табуляции
        /// </summary>
        /// <param name="accept"></param>
        void ApplyAcceptsTab(bool accept);
        /// <summary>
        /// Разрешение возврата каретки (Enter)
        /// </summary>
        /// <param name="accept"></param>
        void ApplyAcceptsReturn(bool  accept);
        /// <summary>
        /// Установка видимости вертикальной полосы прокрутки
        /// </summary>
        /// <param name="visibility"></param>
        void ApplyVerticalScrollBarVisibility(Controls.ScrollBarVisibility visibility);
        /// <summary>
        /// Установка видимости горизонтальной полосы прокрутки
        /// </summary>
        /// <param name="visibility"></param>
        void ApplyHorizontalScrollBarVisibility(Controls.ScrollBarVisibility visibility);
        /// <summary>
        /// Установка контекстного меню
        /// </summary>
        /// <param name="menu"></param>
        void ApplyContextMenu(Controls.ContextMenu menu);
        /// <summary>
        /// Включение поддержки документов
        /// </summary>
        /// <param name="enabled"></param>
        void ApplyDocumentEnabled(bool enabled);
        /// <summary>
        /// Сбрасывает форматирование выделенного текста
        /// </summary>
        void ClearFormatting();


        // Методы для работы с выделением

        /// <summary>
        /// Выделяет весь текст в документе
        /// </summary>
        void SelectAll();
        /// <summary>
        /// Снимает выделение текста
        /// </summary>
        void ClearSelection();
        /// <summary>
        /// Возвращает выделенный текст в виде строки
        /// </summary>
        /// <returns>выделенный текст</returns>
        string GetSelectedTextAsString();
        /// <summary>
        /// Заменяет выделенный текст на указанный
        /// </summary>
        /// <param name="text">новый текст</param>
        void ReplaceSelectedText(string text);



        // Методы для вставки элементов


        /// <summary>
        /// Вставляет гиперссылку с указанным URI и необязательным отображаемым текстом
        /// </summary>
        /// <param name="uri">гиперссылка с указанным URI</param>
        /// <param name="displayText">необязательный текст</param>
        void InsertHyperlink(string uri, string displayText);
        /// <summary>
        /// Вставляет изображение по указанному пути с опциональными размерами
        /// </summary>
        /// <param name="imagePath">путь к изображению</param>
        /// <param name="width">ширина изображения</param>
        /// <param name="height">высота изображения</param>
        void InsertImage(string imagePath, double width = 0, double height = 0);
        /// <summary>
        /// Вставляет новый параграф в текущей позиции курсора
        /// </summary>
        void InsertParagraph();
        /// <summary>
        /// Вставляет разрыв строки
        /// </summary>
        void InsertLineBreak();
        /// <summary>
        /// Вставляет таблицу с указанным количеством строк и столбцов
        /// </summary>
        /// <param name="rows">строк</param>
        /// <param name="columns">столбцов</param>
        void InsertTable(int rows, int columns);


        // Методы для работы с RTF


        /// <summary>
        /// Возвращает содержимое документа в формате RTF
        /// </summary>
        /// <returns>документ в формате RTF</returns>
        string GetRtf();
        /// <summary>
        /// Загружает содержимое документа из строки RTF
        /// </summary>
        /// <param name="rtf">строка RTF</param>
        void LoadRtf(string rtf);
        /// <summary>
        /// Возвращает содержимое документа в виде обычного текста
        /// </summary>
        /// <returns>текст</returns>
        string GetPlainText();
        /// <summary>
        /// Загружает обычный текст в документ
        /// </summary>
        /// <param name="text">текст</param>
        void LoadPlainText(string text);



        // Методы для управления документом


        /// <summary>
        /// Очищает содержимое документа
        /// </summary>
        void ClearDocument();
        /// <summary>
        /// Прокручивает документ к позиции курсора
        /// </summary>
        void ScrollToCaret();
        /// <summary>
        /// Прокручивает документ в конец
        /// </summary>
        void ScrollToEnd();
        /// <summary>
        /// Прокручивает документ в начало
        /// </summary>
        void ScrollToStart();
        /// <summary>
        /// Устанавливает отступы для документа
        /// </summary>
        /// <param name="margin">отступы</param>
        void SetDocumentMargin(Thickness margin);


        // Методы для управления историей изменений


        /// <summary>
        /// Отменяет последнее действие
        /// </summary>
        void Undo();
        /// <summary>
        /// Повторяет отмененное действие
        /// </summary>
        void Redo();



        // Методы для поиска и замены



        /// <summary>
        /// Ищет текст в документе, возвращает true, если найдено
        /// </summary>
        /// <param name="searchText">Строка, которую нужно найти в содержимом документа</param>
        /// <param name="matchCase">учитывать ли регистр символов при поиске</param>
        /// <param name="wholeWord">искать ли только целые слова</param>
        /// <returns></returns>
        bool FindText(string searchText, bool matchCase = false, bool wholeWord = false);
        /// <summary>
        /// Заменяет первое вхождение текста
        /// </summary>
        /// <param name="searchText">Строка, которую нужно найти для замены</param>
        /// <param name="replaceText">Строка, на которую будет заменено найденное вхождение</param>
        /// <param name="matchCase">учитывать ли регистр при поиске</param>
        /// <param name="wholeWord">заменять ли только целые слова</param>
        void ReplaceText(string searchText, string replaceText, bool matchCase = false, bool wholeWord = false);
        /// <summary>
        /// Заменяет все вхождения текста
        /// </summary>
        /// <param name="searchText">Строка, которую нужно найти для замены</param>
        /// <param name="replaceText">Строка, на которую будет заменено найденное вхождение</param>
        /// <param name="matchCase">учитывать ли регистр при поиске</param>
        /// <param name="wholeWord">заменять ли только целые слова</param>
        void ReplaceAllText(string searchText, string replaceText, bool matchCase = false, bool wholeWord = false);



        // Методы для работы с форматированием списков


        /// <summary>
        /// Применяет маркированный список к выделенному тексту
        /// </summary>
        void ApplyBulletedList();
        /// <summary>
        /// Применяет нумерованный список к выделенному тексту
        /// </summary>
        void ApplyNumberedList();
        /// <summary>
        /// Удаляет форматирование списка
        /// </summary>
        void RemoveListFormatting();


        // Методы для работы с увеличением/уменьшением отступов


        /// <summary>
        /// Увеличивает отступ для текущего параграфа
        /// </summary>
        void IncreaseIndent();
        /// <summary>
        /// Уменьшает отступ для текущего параграфа
        /// </summary>
        void DecreaseIndent();


        // Методы для управления фокусом и вводом


        /// <summary>
        /// Устанавливает фокус на элементе управления
        /// </summary>
        void Focus();
        /// <summary>
        /// Вставляет текст в текущей позиции курсора
        /// </summary>
        /// <param name="text">вставляемый текст</param>
        void InsertTextAtCaret(string text);

    }
}
