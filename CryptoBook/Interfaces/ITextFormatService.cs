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
        void InsertImage(Uri imageUri, double width, double height);

        // --- Очистка форматирования ---
        void ClearAllFormatting();

        // --- Работа с выделением ---
        TextRange GetSelectedTextRange();
        void ReplaceSelectedText(string newText);

        // --- Undo/Redo ---
        void Undo();
        void Redo();

        // --- Общие ---
        bool CanUndo { get; }
        bool CanRedo { get; }

        // --- Навигация ---
        void MoveCaretToStart();
        void MoveCaretToEnd();

        // --- Получение состояния ---
        bool IsBold();
        bool IsItalic();
        bool IsUnderline();
        bool IsStrikethrough();
    }
}
