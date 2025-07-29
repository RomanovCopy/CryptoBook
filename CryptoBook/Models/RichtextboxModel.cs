using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using System.Drawing;
using System.Windows.Input;

namespace CryptoBook.Models
{
    internal class RichtextboxModel: ViewModelBase
    {
        private readonly ILifetimeScope scope;
        private readonly IRichTextDocumentContext context;
        private readonly IRichTextBoxService richService;
        private readonly IFlowDocumentService flowService;
        internal FlowDocument Document => context.Document ?? throw new ArgumentNullException(nameof(context.Document));
        internal bool IsBold =>richService.IsBold;
        internal bool IsItalic => richService.IsItalic;
        internal bool IsUnderlined => richService.IsUnderline;
        internal double FontSize =>richService.FontSize;
        internal string FontFamily => richService.FontFamily;


        internal RichtextboxModel(ILifetimeScope _scope)
        {
            scope = _scope;
            richService = scope.Resolve<IRichTextBoxService>();
            flowService = scope.Resolve<IFlowDocumentService>();
        }


        internal bool CanExecute_BoldCommand(object? obj)
        {
            return richService.Selection != null && !richService.Selection.IsEmpty;
        }
        internal void Execute_BoldCommand(object? obj)
        {
            flowService.ToggleBold(richService.Selection);
        }


        internal bool CanExecute_ItalicCommand(object? obj)
        {
            return richService.Selection != null && !richService.Selection.IsEmpty;
        }
        internal void Execute_ItalicCommand(object? obj)
        {
            flowService.ToggleItalic(richService.Selection);
        }


        internal bool CanExecute_UnderlineCommand(object? obj)
        {
            return richService.Selection != null && !richService.Selection.IsEmpty;
        }
        internal void Execute_UnderlineCommand(object? obj)
        {
            flowService.ToggleUnderline(richService.Selection);
        }


        internal bool CanExecute_ClearFormattingCommand(object? obj)
        {
            return richService.Selection != null && !richService.Selection.IsEmpty;
        }
        internal void Execute_ClearFormattingCommand(object? obj)
        {
            flowService.ClearFormatting(richService.Selection);
        }


        internal bool CanExecute_InsertTextCommand(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text);
        }
        internal void Execute_InsertTextCommand(object? obj)
        {
            if (obj is string text)
                richService.InsertTextAtCaret(text);
        }


        internal bool CanExecute_ClearTextCommand(object? obj)
        {
            return true;
        }
        internal void Execute_ClearTextCommand(object? obj)
        {
            flowService.ClearDocument();
        }


        internal bool CanExecute_InsertImageCommand(object? obj)
        {
            return obj is Tuple<string, double, double> tuple && !string.IsNullOrEmpty(tuple.Item1);
        }
        internal void Execute_InsertImageCommand(object? obj)
        {
            if (obj is Tuple<string, double, double> tuple)
                flowService.InsertImageAt(richService.CaretPosition, tuple.Item1, tuple.Item2, tuple.Item3);
        }


        internal bool CanExecute_CopyCommand(object? obj)
        {
            return richService.Selection != null && !richService.Selection.IsEmpty;
        }
        internal void Execute_CopyCommand(object? obj)
        {
            if (richService.Selection != null)
                richService.Copy();
        }

        internal bool CanExecute_CutCommand(object? obj)
        {
            return richService.Selection != null && !richService.Selection.IsEmpty;
        }
        internal void Execute_CutCommand(object? obj)
        {
            if (richService.Selection != null)
                richService.Cut();
        }


        internal bool CanExecute_PasteCommand(object? obj)
        {
            return true;
        }
        internal void Execute_PasteCommand(object? obj)
        {
            if (richService.Selection != null)
                richService.Paste();
        }


        internal bool CanExecute_ChangeFontSizeCommand(object? obj)
        {
            return obj is double size && size > 0;
        }
        internal void Execute_ChangeFontSizeCommand(object? obj)
        {
            if (obj is double size)
                flowService.ApplyFontSize(richService.Selection, size);
        }


        internal bool CanExecute_ChangeFontFamilyCommand(object? obj)
        {
            return obj is string family && !string.IsNullOrEmpty(family);
        }
        internal void Execute_ChangeFontFamilyCommand(object? obj)
        {
            if (obj is string family)
                flowService.ApplyFontFamily(richService.Selection, family);
        }


        internal bool CanExecute_ChangeForegroundColor(object? obj)
        {
            return obj is System.Windows.Media.Color;
        }
        internal void Execute_ChangeForegroundColor(object? obj)
        {
            if (obj is Color color)
                flowService.ApplyForegroundColor(richService.Selection, color);
        }


        internal bool CanExecute_ChangeBackgroundColor(object? obj)
        {
            return obj is System.Windows.Media.Color;
        }
        internal void Execute_ChangeBackgroundColor(object? obj)
        {
            if (obj is Color color)
                flowService.ApplyBackgroundColor(richService.Selection, color);
        }


        internal bool CanExecute_ApplyTextAlignment(object? obj)
        {
            return obj is TextAlignment;
        }
        internal void Execute_ApplyTextAlignment(object? obj)
        {
            if (obj is TextAlignment alignment)
                flowService.ApplyTextAlignment(richService.Selection, alignment);
        }



        internal bool CanExecute_ApplyAcceptsTab(object? obj)
        {
            return obj is bool;
        }
        internal void Execute_ApplyAcceptsTab(object? obj)
        {
            if (obj is bool accept)
                richService.ApplyAcceptsTab(accept);
        }


        internal bool CanExecute_ApplyAcceptsReturn(object? obj)
        {
            return obj is bool;
        }
        internal void Execute_ApplyAcceptsReturn(object? obj)
        {
            if (obj is bool accept)
                richService.ApplyAcceptsReturn(accept);
        }


        internal bool CanExecute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            return obj is System.Windows.Controls.ScrollBarVisibility;
        }
        internal void Execute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            if (obj is System.Windows.Controls.ScrollBarVisibility visibility)
                richService.ApplyVerticalScrollBarVisibility(visibility);
        }


        internal bool CanExecute_ApplyHorizontalScrollBarVisibility(object? obj)
        {
            return obj is System.Windows.Controls.ScrollBarVisibility;
        }
        internal void Execute_ApplyHorizontalScrollBarVisibility(object? obj)
        {
            if (obj is System.Windows.Controls.ScrollBarVisibility visibility)
                richService.ApplyHorizontalScrollBarVisibility(visibility);
        }


        internal bool CanExecute_ApplyContextMenu(object? obj)
        {
            return obj is System.Windows.Controls.ContextMenu;
        }
        internal void Execute_ApplyContextMenu(object? obj)
        {
            if (obj is System.Windows.Controls.ContextMenu menu)
                richService.ApplyContextMenu(menu);
        }



        internal bool CanExecute_ClearFormatting(object? obj)
        {
            return richService.Selection != null && !richService.Selection.IsEmpty;
        }
        internal void Execute_ClearFormatting(object? obj)
        {
            flowService.ClearFormatting(richService.Selection);
        }


        internal bool CanExecute_SelectAll(object? obj)
        {
            return true;
        }
        internal void Execute_SelectAll(object? obj)
        {
            richService.SelectAll();
        }


        internal bool CanExecute_ClearSelection(object? obj)
        {
            return true;
        }
        internal void Execute_ClearSelection(object? obj)
        {
            richService.ClearSelection();
        }


        internal bool CanExecute_GetSelectedTextAsString(object? obj)
        {
            return richService.Selection != null && !richService.Selection.IsEmpty;
        }
        internal void Execute_GetSelectedTextAsString(object? obj)
        {
            var text = flowService.GetPlainText();
            // Можно вернуть или обработать text по необходимости
        }


        internal bool CanExecute_ReplaceSelectedText(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text) && richService.Selection != null && !richService.Selection.IsEmpty;
        }
        internal void Execute_ReplaceSelectedText(object? obj)
        {
            if (obj is string text)
                flowService.ReplaceText(richService.Selection.Text, text, false, false);
        }


        internal bool CanExecute_InsertHyperlink(object? obj)
        {
            return obj is Tuple<string, string> tuple && !string.IsNullOrEmpty(tuple.Item1);
        }
        internal void Execute_InsertHyperlink(object? obj)
        {
            if (obj is Tuple<string, string> tuple)
                flowService.InsertHyperlinkAt(richService.CaretPosition, tuple.Item1, tuple.Item2);
        }


        internal bool CanExecute_InsertParagraph(object? obj)
        {
            return true;
        }
        internal void Execute_InsertParagraph(object? obj)
        {
            flowService.InsertParagraphAt(richService.CaretPosition);
        }


        internal bool CanExecute_InsertTable(object? obj)
        {
            return obj is Tuple<int, int> tuple && tuple.Item1 > 0 && tuple.Item2 > 0;
        }
        internal void Execute_InsertTable(object? obj)
        {
            if (obj is Tuple<int, int> tuple)
                flowService.InsertTableAt(richService.CaretPosition, tuple.Item1, tuple.Item2);
        }


        internal bool CanExecute_GetRtf(object? obj)
        {
            return true;
        }
        internal void Execute_GetRtf(object? obj)
        {
            var rtf = flowService.GetRtf();
            // Можно вернуть или обработать rtf по необходимости
        }


        internal bool CanExecute_LoadRtf(object? obj)
        {
            return obj is string rtf && !string.IsNullOrEmpty(rtf);
        }
        internal void Execute_LoadRtf(object? obj)
        {
            if (obj is string rtf)
                flowService.LoadRtf(rtf);
        }



        internal bool CanExecute_LoadPlainText(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text);
        }
        internal void Execute_LoadPlainText(object? obj)
        {
            if (obj is string text)
                flowService.LoadPlainText(text);
        }


        internal bool CanExecute_ClearDocument(object? obj)
        {
            return true;
        }
        internal void Execute_ClearDocument(object? obj)
        {
            flowService.ClearDocument();
        }


        internal bool CanExecute_ScrollToCaret(object? obj)
        {
            return true;
        }
        internal void Execute_ScrollToCaret(object? obj)
        {
            richService.ScrollToCaret();
        }


        internal bool CanExecute_ScrollToEnd(object? obj)
        {
            return true;
        }
        internal void Execute_ScrollToEnd(object? obj)
        {
            richService.ScrollToEnd();
        }


        internal bool CanExecute_ScrollToStart(object? obj)
        {
            return true;
        }
        internal void Execute_ScrollToStart(object? obj)
        {
            richService.ScrollToStart();
        }


        internal bool CanExecute_SetDocumentMargin(object? obj)
        {
            return obj is System.Windows.Thickness;
        }
        internal void Execute_SetDocumentMargin(object? obj)
        {
            if (obj is System.Windows.Thickness margin)
                flowService.SetDocumentMargin(margin);
        }


        internal bool CanExecute_Undo(object? obj)
        {
            return richService.CanUndo;
        }
        internal void Execute_Undo(object? obj)
        {
            richService.Undo();
        }


        internal bool CanExecute_Redo(object? obj)
        {
            return richService.CanRedo;
        }
        internal void Execute_Redo(object? obj)
        {
            richService.Redo();
        }


        internal bool CanExecute_FindText(object? obj)
        {
            return obj is Tuple<string, bool, bool> tuple && !string.IsNullOrEmpty(tuple.Item1);
        }
        internal void Execute_FindText(object? obj)
        {
            if (obj is Tuple<string, bool, bool> tuple)
                flowService.FindText(tuple.Item1, tuple.Item2, tuple.Item3);
        }


        internal bool CanExecute_ReplaceText(object? obj)
        {
            return obj is Tuple<string, string, bool, bool> tuple && !string.IsNullOrEmpty(tuple.Item1) && !string.IsNullOrEmpty(tuple.Item2);
        }
        internal void Execute_ReplaceText(object? obj)
        {
            if (obj is Tuple<string, string, bool, bool> tuple)
                flowService.ReplaceText(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
        }


        internal bool CanExecute_ReplaceAllText(object? obj)
        {
            return obj is Tuple<string, string, bool, bool> tuple && !string.IsNullOrEmpty(tuple.Item1) && !string.IsNullOrEmpty(tuple.Item2);
        }
        internal void Execute_ReplaceAllText(object? obj)
        {
            if (obj is Tuple<string, string, bool, bool> tuple)
                flowService.ReplaceAllText(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
        }


        internal bool CanExecute_ApplyBulletedList(object? obj)
        {
            return true;
        }
        internal void Execute_ApplyBulletedList(object? obj)
        {
            flowService.ApplyBulletedList(richService.Selection);
        }


        internal bool CanExecute_ApplyNumberedList(object? obj)
        {
            return true;
        }
        internal void Execute_ApplyNumberedList(object? obj)
        {
            flowService.ApplyNumberedList(richService.Selection);
        }


        internal bool CanExecute_RemoveListFormatting(object? obj)
        {
            return true;
        }
        internal void Execute_RemoveListFormatting(object? obj)
        {
            flowService.RemoveListFormatting(richService.Selection);
        }


        internal bool CanExecute_IncreaseIndent(object? obj)
        {
            return true;
        }
        internal void Execute_IncreaseIndent(object? obj)
        {
            flowService.IncreaseIndent(richService.Selection);
        }


        internal bool CanExecute_DecreaseIndent(object? obj)
        {
            return true;
        }
        internal void Execute_DecreaseIndent(object? obj)
        {
            flowService.DecreaseIndent(richService.Selection);
        }


        internal bool CanExecute_Focus(object? obj)
        {
            return true;
        }
        internal void Execute_Focus(object? obj)
        {
            richService.Focus();
        }


        internal bool CanExecute_InsertTextAtCaret(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text);
        }
        internal void Execute_InsertTextAtCaret(object? obj)
        {
            if (obj is string text)
                richService.InsertTextAtCaret(text);
        }
        internal bool CanExecute_InsertLineBreak(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_InsertLineBreak(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
        }
        internal bool CanExecute_Close(object? obj)
        {
            return true;
        }
        internal void Execute_Close(object? obj)
        {
            // Здесь можно реализовать логику закрытия, если требуется
        }
        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
        {
            // Здесь можно реализовать логику при закрытии, если требуется
        }
        internal bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        internal void Execute_Closed(object? obj)
        {
            // Здесь можно реализовать логику после закрытия, если требуется
        }




    }
}
