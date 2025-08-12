using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using Media = System.Windows.Media;


namespace CryptoBook.Interfaces
{
    public interface IParagraphService
    {

        // Доступ к реальному WPF-элементу, чтобы добавить в FlowDocument
        Paragraph Element { get; }


        // --- Свойства Paragraph ---

        /// <summary>
        /// Отступ слева.
        /// </summary>
        Thickness Margin { get; set; }

        /// <summary>
        /// Отступ слева в двойных единицах (Double).
        /// </summary>
        double TextIndent { get; set; }

        /// <summary>
        /// Выравнивание текста в параграфе.
        /// </summary>
        TextAlignment TextAlignment { get; set; }

        /// <summary>
        /// Межстрочный интервал.
        /// </summary>
        double LineHeight { get; set; }

        /// <summary>
        /// Тип межстрочного интервала (напр. Auto, Exactly, etc).
        /// </summary>
        LineStackingStrategy LineStackingStrategy { get; set; }

        /// <summary>
        /// Перенос строки внутри параграфа (если разрешён).
        /// </summary>
        bool BreakPageBefore { get; set; }

        /// <summary>
        /// Цвет фона параграфа.
        /// </summary>
        Media.Brush Background { get; set; }

        /// <summary>
        /// Цвет текста параграфа.
        /// </summary>
        Media.Brush Foreground { get; set; }

        /// <summary>
        /// Коллекция Inline-элементов, составляющих содержимое параграфа.
        /// </summary>
        InlineCollection Inlines { get; }

        /// <summary>
        /// Родительский блок документа.
        /// </summary>
        TextElement Parent { get; }

        /// <summary>
        /// Родительский документ FlowDocument.
        /// </summary>
        FlowDocument FlowDocumentParent { get; }

        // --- Методы Paragraph ---

        /// <summary>
        /// Добавляет Inline элемент в параграф.
        /// </summary>
        /// <param name="inline">Inline элемент (Run, Span, etc).</param>
        void AddInline(Inline inline);

        /// <summary>
        /// Удаляет Inline элемент из параграфа.
        /// </summary>
        /// <param name="inline">Inline элемент для удаления.</param>
        /// <returns>True, если удаление прошло успешно.</returns>
        bool RemoveInline(Inline inline);

        /// <summary>
        /// Клонирует параграф.
        /// </summary>
        /// <returns>Новый объект Paragraph с копией содержимого.</returns>
        IParagraphService Clone();

        /// <summary>
        /// Очистить содержимое параграфа.
        /// </summary>
        void Clear();

        /// <summary>
        /// Установить стиль параграфа.
        /// </summary>
        /// <param name="style">Объект стиля.</param>
        void SetStyle(Style style);

        /// <summary>
        /// Получить стиль параграфа.
        /// </summary>
        /// <returns>Текущий стиль.</returns>
        Style GetStyle();

        /// <summary>
        /// Проверить, содержит ли параграф указанный Inline элемент.
        /// </summary>
        /// <param name="inline">Inline элемент для проверки.</param>
        /// <returns>True, если элемент содержится.</returns>
        bool ContainsInline(Inline inline);

        /// <summary>
        /// Применить Brush для заливки текста.
        /// </summary>
        /// <param name="brush">Brush для заливки.</param>
        void ApplyForeground(Media.Brush brush);

        /// <summary>
        /// Применить Brush для фона параграфа.
        /// </summary>
        /// <param name="brush">Brush для фона.</param>
        void ApplyBackground(Media.Brush brush);


        // Контент
        void AddText(string text);
        void AddInlines(params Inline[] inlines);


        /// <summary>
        /// Копирование форматирования
        /// </summary>
        /// <param name="other"></param>
        /// <param name="copyOnlyLocal"></param>
        void CopyFormattingFrom(IParagraphService state, bool copyOnlyLocal);

    }

}
