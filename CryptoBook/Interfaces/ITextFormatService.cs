using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface ITextFormatService
    {
        // --- Выравнивание ---
        void SetTextAlignment(TextAlignment alignment);
        void SetParagraphIndent(double indent);
        void SetLineHeight(double lineHeight);
        void SetLineSpacing(double spacing);

        // --- Списки ---
        void ToggleBulletList();
        void ToggleNumberedList();

        // --- Вставка элементов ---
        void InsertHyperlink(string url, string displayText);

        // --- Очистка форматирования ---
        void ClearAllFormatting();

        // --- Работа с выделением ---
        TextRange GetSelectedTextRange();
        void ReplaceSelectedText(string newText);

        /// <summary>
        /// межстрочный интервал
        /// </summary>
        double LineHeight { get; set; }

        // --- Undo/Redo ---
        void Undo();
        void Redo();

        // --- Общие ---
        bool CanUndo { get; }
        bool CanRedo { get; }

        // --- Навигация ---
        void MoveCaretToStart();
        void MoveCaretToEnd();

    }
}
