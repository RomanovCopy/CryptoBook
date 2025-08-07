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
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;


namespace CryptoBook.Interfaces
{
    public interface IRichTextBoxService
    {
        Controls.RichTextBox Service { get; } // если нужно получить доступ к контролу

        FlowDocument Document { get; }
        TextSelection Selection { get; }
        TextPointer CaretPosition { get; set; }

        Media.Brush CaretBrush {  get; set; }
        Media.Brush BackGround {  get; set; }


        bool IsReadOnly { get; set; }
        bool SpellCheckEnabled { get; set; }
        bool CanUndo { get; }
        bool CanRedo { get; }

        // Навигация и фокус
        void Focus();
        void ScrollToCaret();
        void ScrollToStart();
        void ScrollToEnd();

        // Работа с буфером обмена
        void Copy();
        void Cut();
        void Paste();

        // Выделение
        void SelectAll();
        void ClearSelection();
        void RestoreSelection();

        // Ввод текста
        void InsertTextAtCaret(string text);

        // История изменений
        void Undo();
        void Redo();



        // UI-настройки
        void ApplyVerticalScrollBarVisibility(ScrollBarVisibility visibility);
        void ApplyHorizontalScrollBarVisibility(ScrollBarVisibility visibility);
        void ApplyContextMenu(ContextMenu menu);
        void ApplyAcceptsTab(bool accept);
        void ApplyAcceptsReturn(bool accept);


        double GetFontSizeInSelection();
    }
}
