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

namespace CryptoBook.Models
{
    internal class RichtextboxModel: ViewModelBase
    {
        private readonly ILifetimeScope scope;
        private readonly IRichTextBoxService richTextBoxService;
        private readonly IFlowDocumentService flowDocumentService;
        internal FlowDocument Document 
        { 
            get=>flowDocumentService.Document; 
            set=>flowDocumentService.Document = value; 
        }
        internal bool IsBold { get=>isBold; set=>SetProperty(ref isBold,value); }
        bool isBold;
        internal bool IsItalic { get=> isItalic; set=>SetProperty(ref isItalic, value); }
        bool isItalic;
        internal bool IsUnderlined { get=> isUnderlined; set=>SetProperty(ref isUnderlined, value); }
        bool isUnderlined;
        internal double FontSize { get=> fontSize; set=>SetProperty(ref fontSize, value); }
        double fontSize;


        internal RichtextboxModel(ILifetimeScope _scope)
        {
            scope = _scope;
            richTextBoxService = scope.Resolve<IRichTextBoxService>();
            flowDocumentService = scope.Resolve<IFlowDocumentService>();
        }


        internal bool CanExecute_BoldCommand(object? obj)
        {
            return richTextBoxService.Selection != null && !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_BoldCommand(object? obj)
        {
            flowDocumentService.ToggleBold(richTextBoxService.Selection);
        }


        internal bool CanExecute_ItalicCommand(object? obj)
        {
            return richTextBoxService.Selection != null && !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_ItalicCommand(object? obj)
        {
            flowDocumentService.ToggleItalic(richTextBoxService.Selection);
        }


        internal bool CanExecute_UnderlineCommand(object? obj)
        {
            return richTextBoxService.Selection != null && !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_UnderlineCommand(object? obj)
        {
            flowDocumentService.ToggleUnderline(richTextBoxService.Selection);
        }


        internal bool CanExecute_ClearFormattingCommand(object? obj)
        {
            return richTextBoxService.Selection != null && !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_ClearFormattingCommand(object? obj)
        {
            flowDocumentService.ClearFormatting(richTextBoxService.Selection);
        }


        internal bool CanExecute_InsertTextCommand(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text);
        }
        internal void Execute_InsertTextCommand(object? obj)
        {
            if (obj is string text)
                richTextBoxService.InsertTextAtCaret(text);
        }


        internal bool CanExecute_ClearTextCommand(object? obj)
        {
            return true;
        }
        internal void Execute_ClearTextCommand(object? obj)
        {
            flowDocumentService.ClearDocument();
        }


        internal bool CanExecute_InsertImageCommand(object? obj)
        {
            return obj is Tuple<string, double, double> tuple && !string.IsNullOrEmpty(tuple.Item1);
        }
        internal void Execute_InsertImageCommand(object? obj)
        {
            if (obj is Tuple<string, double, double> tuple)
                flowDocumentService.InsertImageAt(richTextBoxService.CaretPosition, tuple.Item1, tuple.Item2, tuple.Item3);
        }


        internal bool CanExecute_CopyCommand(object? obj)
        {
            return richTextBoxService.Selection != null && !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_CopyCommand(object? obj)
        {
            if (richTextBoxService.Selection != null)
                richTextBoxService.Copy();
        }

        internal bool CanExecute_CutCommand(object? obj)
        {
            return richTextBoxService.Selection != null && !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_CutCommand(object? obj)
        {
            if (richTextBoxService.Selection != null)
                richTextBoxService.Cut();
        }


        internal bool CanExecute_PasteCommand(object? obj)
        {
            return true;
        }
        internal void Execute_PasteCommand(object? obj)
        {
            if (richTextBoxService.Selection != null)
                richTextBoxService.Paste();
        }


        internal bool CanExecute_ChangeFontSizeCommand(object? obj)
        {
            return obj is double size && size > 0;
        }
        internal void Execute_ChangeFontSizeCommand(object? obj)
        {
            if (obj is double size)
                flowDocumentService.ApplyFontSize(richTextBoxService.Selection, size);
        }


        internal bool CanExecute_ChangeFontFamilyCommand(object? obj)
        {
            return obj is string family && !string.IsNullOrEmpty(family);
        }
        internal void Execute_ChangeFontFamilyCommand(object? obj)
        {
            if (obj is string family)
                flowDocumentService.ApplyFontFamily(richTextBoxService.Selection, family);
        }


        internal bool CanExecute_ChangeForegroundColor(object? obj)
        {
            return obj is System.Windows.Media.Color;
        }
        internal void Execute_ChangeForegroundColor(object? obj)
        {
            if (obj is Color color)
                flowDocumentService.ApplyForegroundColor(richTextBoxService.Selection, color);
        }


        internal bool CanExecute_ChangeBackgroundColor(object? obj)
        {
            return obj is System.Windows.Media.Color;
        }
        internal void Execute_ChangeBackgroundColor(object? obj)
        {
            if (obj is Color color)
                flowDocumentService.ApplyBackgroundColor(richTextBoxService.Selection, color);
        }


        internal bool CanExecute_ApplyTextAlignment(object? obj)
        {
            return obj is TextAlignment;
        }
        internal void Execute_ApplyTextAlignment(object? obj)
        {
            if (obj is TextAlignment alignment)
                flowDocumentService.ApplyTextAlignment(richTextBoxService.Selection, alignment);
        }



        internal bool CanExecute_ApplyAcceptsTab(object? obj)
        {
            return obj is bool;
        }
        internal void Execute_ApplyAcceptsTab(object? obj)
        {
            if (obj is bool accept)
                richTextBoxService.ApplyAcceptsTab(accept);
        }


        internal bool CanExecute_ApplyAcceptsReturn(object? obj)
        {
            return obj is bool;
        }
        internal void Execute_ApplyAcceptsReturn(object? obj)
        {
            if (obj is bool accept)
                richTextBoxService.ApplyAcceptsReturn(accept);
        }


        internal bool CanExecute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            return obj is System.Windows.Controls.ScrollBarVisibility;
        }
        internal void Execute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            if (obj is System.Windows.Controls.ScrollBarVisibility visibility)
                richTextBoxService.ApplyVerticalScrollBarVisibility(visibility);
        }


        internal bool CanExecute_ApplyHorizontalScrollBarVisibility(object? obj)
        {
            return obj is System.Windows.Controls.ScrollBarVisibility;
        }
        internal void Execute_ApplyHorizontalScrollBarVisibility(object? obj)
        {
            if (obj is System.Windows.Controls.ScrollBarVisibility visibility)
                richTextBoxService.ApplyHorizontalScrollBarVisibility(visibility);
        }


        internal bool CanExecute_ApplyContextMenu(object? obj)
        {
            return obj is System.Windows.Controls.ContextMenu;
        }
        internal void Execute_ApplyContextMenu(object? obj)
        {
            if (obj is System.Windows.Controls.ContextMenu menu)
                richTextBoxService.ApplyContextMenu(menu);
        }



        internal bool CanExecute_ClearFormatting(object? obj)
        {
            return richTextBoxService.Selection != null && !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_ClearFormatting(object? obj)
        {
            flowDocumentService.ClearFormatting(richTextBoxService.Selection);
        }


        internal bool CanExecute_SelectAll(object? obj)
        {
            return true;
        }
        internal void Execute_SelectAll(object? obj)
        {
            richTextBoxService.SelectAll();
        }


        internal bool CanExecute_ClearSelection(object? obj)
        {
            return true;
        }
        internal void Execute_ClearSelection(object? obj)
        {
            richTextBoxService.ClearSelection();
        }


        internal bool CanExecute_GetSelectedTextAsString(object? obj)
        {
            return richTextBoxService.Selection != null && !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_GetSelectedTextAsString(object? obj)
        {
            var text = flowDocumentService.GetPlainText();
            // Можно вернуть или обработать text по необходимости
        }


        internal bool CanExecute_ReplaceSelectedText(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text) && richTextBoxService.Selection != null && !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_ReplaceSelectedText(object? obj)
        {
            if (obj is string text)
                flowDocumentService.ReplaceText(richTextBoxService.Selection.Text, text, false, false);
        }


        internal bool CanExecute_InsertHyperlink(object? obj)
        {
            return obj is Tuple<string, string> tuple && !string.IsNullOrEmpty(tuple.Item1);
        }
        internal void Execute_InsertHyperlink(object? obj)
        {
            if (obj is Tuple<string, string> tuple)
                flowDocumentService.InsertHyperlinkAt(richTextBoxService.CaretPosition, tuple.Item1, tuple.Item2);
        }


        internal bool CanExecute_InsertParagraph(object? obj)
        {
            return true;
        }
        internal void Execute_InsertParagraph(object? obj)
        {
            flowDocumentService.InsertParagraphAt(richTextBoxService.CaretPosition);
        }


        internal bool CanExecute_InsertTable(object? obj)
        {
            return obj is Tuple<int, int> tuple && tuple.Item1 > 0 && tuple.Item2 > 0;
        }
        internal void Execute_InsertTable(object? obj)
        {
            if (obj is Tuple<int, int> tuple)
                flowDocumentService.InsertTableAt(richTextBoxService.CaretPosition, tuple.Item1, tuple.Item2);
        }


        internal bool CanExecute_GetRtf(object? obj)
        {
            return true;
        }
        internal void Execute_GetRtf(object? obj)
        {
            var rtf = flowDocumentService.GetRtf();
            // Можно вернуть или обработать rtf по необходимости
        }


        internal bool CanExecute_LoadRtf(object? obj)
        {
            return obj is string rtf && !string.IsNullOrEmpty(rtf);
        }
        internal void Execute_LoadRtf(object? obj)
        {
            if (obj is string rtf)
                flowDocumentService.LoadRtf(rtf);
        }



        internal bool CanExecute_LoadPlainText(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text);
        }
        internal void Execute_LoadPlainText(object? obj)
        {
            if (obj is string text)
                flowDocumentService.LoadPlainText(text);
        }


        internal bool CanExecute_ClearDocument(object? obj)
        {
            return true;
        }
        internal void Execute_ClearDocument(object? obj)
        {
            flowDocumentService.ClearDocument();
        }


        internal bool CanExecute_ScrollToCaret(object? obj)
        {
            return true;
        }
        internal void Execute_ScrollToCaret(object? obj)
        {
            richTextBoxService.ScrollToCaret();
        }


        internal bool CanExecute_ScrollToEnd(object? obj)
        {
            return true;
        }
        internal void Execute_ScrollToEnd(object? obj)
        {
            richTextBoxService.ScrollToEnd();
        }


        internal bool CanExecute_ScrollToStart(object? obj)
        {
            return true;
        }
        internal void Execute_ScrollToStart(object? obj)
        {
            richTextBoxService.ScrollToStart();
        }


        internal bool CanExecute_SetDocumentMargin(object? obj)
        {
            return obj is System.Windows.Thickness;
        }
        internal void Execute_SetDocumentMargin(object? obj)
        {
            if (obj is System.Windows.Thickness margin)
                flowDocumentService.SetDocumentMargin(margin);
        }


        internal bool CanExecute_Undo(object? obj)
        {
            return richTextBoxService.CanUndo;
        }
        internal void Execute_Undo(object? obj)
        {
            richTextBoxService.Undo();
        }


        internal bool CanExecute_Redo(object? obj)
        {
            return richTextBoxService.CanRedo;
        }
        internal void Execute_Redo(object? obj)
        {
            richTextBoxService.Redo();
        }


        internal bool CanExecute_FindText(object? obj)
        {
            return obj is Tuple<string, bool, bool> tuple && !string.IsNullOrEmpty(tuple.Item1);
        }
        internal void Execute_FindText(object? obj)
        {
            if (obj is Tuple<string, bool, bool> tuple)
                flowDocumentService.FindText(tuple.Item1, tuple.Item2, tuple.Item3);
        }


        internal bool CanExecute_ReplaceText(object? obj)
        {
            return obj is Tuple<string, string, bool, bool> tuple && !string.IsNullOrEmpty(tuple.Item1) && !string.IsNullOrEmpty(tuple.Item2);
        }
        internal void Execute_ReplaceText(object? obj)
        {
            if (obj is Tuple<string, string, bool, bool> tuple)
                flowDocumentService.ReplaceText(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
        }


        internal bool CanExecute_ReplaceAllText(object? obj)
        {
            return obj is Tuple<string, string, bool, bool> tuple && !string.IsNullOrEmpty(tuple.Item1) && !string.IsNullOrEmpty(tuple.Item2);
        }
        internal void Execute_ReplaceAllText(object? obj)
        {
            if (obj is Tuple<string, string, bool, bool> tuple)
                flowDocumentService.ReplaceAllText(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
        }


        internal bool CanExecute_ApplyBulletedList(object? obj)
        {
            return true;
        }
        internal void Execute_ApplyBulletedList(object? obj)
        {
            flowDocumentService.ApplyBulletedList(richTextBoxService.Selection);
        }


        internal bool CanExecute_ApplyNumberedList(object? obj)
        {
            return true;
        }
        internal void Execute_ApplyNumberedList(object? obj)
        {
            flowDocumentService.ApplyNumberedList(richTextBoxService.Selection);
        }


        internal bool CanExecute_RemoveListFormatting(object? obj)
        {
            return true;
        }
        internal void Execute_RemoveListFormatting(object? obj)
        {
            flowDocumentService.RemoveListFormatting(richTextBoxService.Selection);
        }


        internal bool CanExecute_IncreaseIndent(object? obj)
        {
            return true;
        }
        internal void Execute_IncreaseIndent(object? obj)
        {
            flowDocumentService.IncreaseIndent(richTextBoxService.Selection);
        }


        internal bool CanExecute_DecreaseIndent(object? obj)
        {
            return true;
        }
        internal void Execute_DecreaseIndent(object? obj)
        {
            flowDocumentService.DecreaseIndent(richTextBoxService.Selection);
        }


        internal bool CanExecute_Focus(object? obj)
        {
            return true;
        }
        internal void Execute_Focus(object? obj)
        {
            richTextBoxService.Focus();
        }


        internal bool CanExecute_InsertTextAtCaret(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text);
        }
        internal void Execute_InsertTextAtCaret(object? obj)
        {
            if (obj is string text)
                richTextBoxService.InsertTextAtCaret(text);
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
