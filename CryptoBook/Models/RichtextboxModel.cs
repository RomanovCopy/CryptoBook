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

namespace CryptoBook.Models
{
    internal class RichtextboxModel: ViewModelBase
    {
        private readonly ILifetimeScope scope;
        private readonly IRichTextBoxService service;
        internal FlowDocument Document { get=>document; set=>SetProperty(ref document, value); }
        FlowDocument document;
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
            service = scope.Resolve<IRichTextBoxService>();
        }


        internal bool CanExecute_BoldCommand(object? obj)
        {
            return service.Selection != null && !service.Selection.IsEmpty;
        }
        internal void Execute_BoldCommand(object? obj)
        {
            service.ApplyBold();
        }


        internal bool CanExecute_ItalicCommand(object? obj)
        {
            return service.Selection != null && !service.Selection.IsEmpty;
        }
        internal void Execute_ItalicCommand(object? obj)
        {
            service.ApplyItalic();
        }


        internal bool CanExecute_UnderlineCommand(object? obj)
        {
            return service.Selection != null && !service.Selection.IsEmpty;
        }
        internal void Execute_UnderlineCommand(object? obj)
        {
            service.ApplyUnderline();
        }


        internal bool CanExecute_ClearFormattingCommand(object? obj)
        {
            return service.Selection != null && !service.Selection.IsEmpty;
        }
        internal void Execute_ClearFormattingCommand(object? obj)
        {
            service.ClearFormatting();
        }


        internal bool CanExecute_InsertTextCommand(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text);
        }
        internal void Execute_InsertTextCommand(object? obj)
        {
            if (obj is string text)
                service.InsertTextAtCaret(text);
        }


        internal bool CanExecute_ClearTextCommand(object? obj)
        {
            return true;
        }
        internal void Execute_ClearTextCommand(object? obj)
        {
            service.ClearDocument();
        }


        internal bool CanExecute_InsertImageCommand(object? obj)
        {
            return obj is Tuple<string, double, double> tuple && !string.IsNullOrEmpty(tuple.Item1);
        }
        internal void Execute_InsertImageCommand(object? obj)
        {
            if (obj is Tuple<string, double, double> tuple)
                service.InsertImage(tuple.Item1, tuple.Item2, tuple.Item3);
        }


        internal bool CanExecute_CopyCommand(object? obj)
        {
            return service.Selection != null && !service.Selection.IsEmpty;
        }
        internal void Execute_CopyCommand(object? obj)
        {
            if (service.Selection != null)
                service.Copy();
        }

        internal bool CanExecute_CutCommand(object? obj)
        {
            return service.Selection != null && !service.Selection.IsEmpty;
        }
        internal void Execute_CutCommand(object? obj)
        {
            if (service.Selection != null)
                service.Cut();
        }


        internal bool CanExecute_PasteCommand(object? obj)
        {
            return true;
        }
        internal void Execute_PasteCommand(object? obj)
        {
            if (service.Selection != null)
                service.Paste();
        }


        internal bool CanExecute_ChangeFontSizeCommand(object? obj)
        {
            return obj is double size && size > 0;
        }
        internal void Execute_ChangeFontSizeCommand(object? obj)
        {
            if (obj is double size)
                service.ApplyFontSize(size);
        }


        internal bool CanExecute_ChangeFontFamilyCommand(object? obj)
        {
            return obj is string family && !string.IsNullOrEmpty(family);
        }
        internal void Execute_ChangeFontFamilyCommand(object? obj)
        {
            if (obj is string family)
                service.ApplyFontFamily(family);
        }


        internal bool CanExecute_ChangeForegroundColor(object? obj)
        {
            return obj is System.Windows.Media.Color;
        }
        internal void Execute_ChangeForegroundColor(object? obj)
        {
            if (obj is System.Windows.Media.Color color)
                service.ApplyForegroundColor(color);
        }


        internal bool CanExecute_ChangeBackgroundColor(object? obj)
        {
            return obj is System.Windows.Media.Color;
        }
        internal void Execute_ChangeBackgroundColor(object? obj)
        {
            if (obj is System.Windows.Media.Color color)
                service.ApplyBackgroundColor(color);
        }


        internal bool CanExecute_ApplyTextAlignment(object? obj)
        {
            return obj is TextAlignment;
        }
        internal void Execute_ApplyTextAlignment(object? obj)
        {
            if (obj is TextAlignment alignment)
                service.ApplyTextAlignment(alignment);
        }


        internal bool CanExecute_ApplyTextFormattingMode(object? obj)
        {
            return obj is System.Windows.Media.TextFormattingMode;
        }
        internal void Execute_ApplyTextFormattingMode(object? obj)
        {
            if (obj is System.Windows.Media.TextFormattingMode mode)
                service.ApplyTextFormattingMode(mode);
        }


        internal bool CanExecute_ApplyTextRenderingMode(object? obj)
        {
            return obj is System.Windows.Media.TextRenderingMode;
        }
        internal void Execute_ApplyTextRenderingMode(object? obj)
        {
            if (obj is System.Windows.Media.TextRenderingMode mode)
                service.ApplyTextRenderingMode(mode);
        }


        internal bool CanExecute_ApplyAcceptsTab(object? obj)
        {
            return obj is bool;
        }
        internal void Execute_ApplyAcceptsTab(object? obj)
        {
            if (obj is bool accept)
                service.ApplyAcceptsTab(accept);
        }


        internal bool CanExecute_ApplyAcceptsReturn(object? obj)
        {
            return obj is bool;
        }
        internal void Execute_ApplyAcceptsReturn(object? obj)
        {
            if (obj is bool accept)
                service.ApplyAcceptsReturn(accept);
        }


        internal bool CanExecute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            return obj is System.Windows.Controls.ScrollBarVisibility;
        }
        internal void Execute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            if (obj is System.Windows.Controls.ScrollBarVisibility visibility)
                service.ApplyVerticalScrollBarVisibility(visibility);
        }


        internal bool CanExecute_ApplyHorizontalScrollBarVisibility(object? obj)
        {
            return obj is System.Windows.Controls.ScrollBarVisibility;
        }
        internal void Execute_ApplyHorizontalScrollBarVisibility(object? obj)
        {
            if (obj is System.Windows.Controls.ScrollBarVisibility visibility)
                service.ApplyHorizontalScrollBarVisibility(visibility);
        }


        internal bool CanExecute_ApplyContextMenu(object? obj)
        {
            return obj is System.Windows.Controls.ContextMenu;
        }
        internal void Execute_ApplyContextMenu(object? obj)
        {
            if (obj is System.Windows.Controls.ContextMenu menu)
                service.ApplyContextMenu(menu);
        }


        internal bool CanExecute_ApplyDocumentEnabled(object? obj)
        {
            return obj is bool;
        }
        internal void Execute_ApplyDocumentEnabled(object? obj)
        {
            if (obj is bool enabled)
                service.ApplyDocumentEnabled(enabled);
        }


        internal bool CanExecute_ClearFormatting(object? obj)
        {
            return service.Selection != null && !service.Selection.IsEmpty;
        }
        internal void Execute_ClearFormatting(object? obj)
        {
            service.ClearFormatting();
        }


        internal bool CanExecute_SelectAll(object? obj)
        {
            return true;
        }
        internal void Execute_SelectAll(object? obj)
        {
            service.SelectAll();
        }


        internal bool CanExecute_ClearSelection(object? obj)
        {
            return true;
        }
        internal void Execute_ClearSelection(object? obj)
        {
            service.ClearSelection();
        }


        internal bool CanExecute_GetSelectedTextAsString(object? obj)
        {
            return service.Selection != null && !service.Selection.IsEmpty;
        }
        internal void Execute_GetSelectedTextAsString(object? obj)
        {
            var text = service.GetSelectedTextAsString();
            // Можно вернуть или обработать text по необходимости
        }


        internal bool CanExecute_ReplaceSelectedText(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text) && service.Selection != null && !service.Selection.IsEmpty;
        }
        internal void Execute_ReplaceSelectedText(object? obj)
        {
            if (obj is string text)
                service.ReplaceSelectedText(text);
        }


        internal bool CanExecute_InsertHyperlink(object? obj)
        {
            return obj is Tuple<string, string> tuple && !string.IsNullOrEmpty(tuple.Item1);
        }
        internal void Execute_InsertHyperlink(object? obj)
        {
            if (obj is Tuple<string, string> tuple)
                service.InsertHyperlink(tuple.Item1, tuple.Item2);
        }


        internal bool CanExecute_InsertParagraph(object? obj)
        {
            return true;
        }
        internal void Execute_InsertParagraph(object? obj)
        {
            service.InsertParagraph();
        }


        internal bool CanExecute_InsertTable(object? obj)
        {
            return obj is Tuple<int, int> tuple && tuple.Item1 > 0 && tuple.Item2 > 0;
        }
        internal void Execute_InsertTable(object? obj)
        {
            if (obj is Tuple<int, int> tuple)
                service.InsertTable(tuple.Item1, tuple.Item2);
        }


        internal bool CanExecute_GetRtf(object? obj)
        {
            return true;
        }
        internal void Execute_GetRtf(object? obj)
        {
            var rtf = service.GetRtf();
            // Можно вернуть или обработать rtf по необходимости
        }


        internal bool CanExecute_LoadRtf(object? obj)
        {
            return obj is string rtf && !string.IsNullOrEmpty(rtf);
        }
        internal void Execute_LoadRtf(object? obj)
        {
            if (obj is string rtf)
                service.LoadRtf(rtf);
        }


        internal bool CanExecute_GetPlainText(object? obj)
        {
            return true;
        }
        internal void Execute_GetPlainText(object? obj)
        {
            var text = service.GetPlainText();
            // Можно вернуть или обработать text по необходимости
        }


        internal bool CanExecute_LoadPlainText(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text);
        }
        internal void Execute_LoadPlainText(object? obj)
        {
            if (obj is string text)
                service.LoadPlainText(text);
        }


        internal bool CanExecute_ClearDocument(object? obj)
        {
            return true;
        }
        internal void Execute_ClearDocument(object? obj)
        {
            service.ClearDocument();
        }


        internal bool CanExecute_ScrollToCaret(object? obj)
        {
            return true;
        }
        internal void Execute_ScrollToCaret(object? obj)
        {
            service.ScrollToCaret();
        }


        internal bool CanExecute_ScrollToEnd(object? obj)
        {
            return true;
        }
        internal void Execute_ScrollToEnd(object? obj)
        {
            service.ScrollToEnd();
        }


        internal bool CanExecute_ScrollToStart(object? obj)
        {
            return true;
        }
        internal void Execute_ScrollToStart(object? obj)
        {
            service.ScrollToStart();
        }


        internal bool CanExecute_SetDocumentMargin(object? obj)
        {
            return obj is System.Windows.Thickness;
        }
        internal void Execute_SetDocumentMargin(object? obj)
        {
            if (obj is System.Windows.Thickness margin)
                service.SetDocumentMargin(margin);
        }


        internal bool CanExecute_Undo(object? obj)
        {
            return service.CanUndo;
        }
        internal void Execute_Undo(object? obj)
        {
            service.Undo();
        }


        internal bool CanExecute_Redo(object? obj)
        {
            return service.CanRedo;
        }
        internal void Execute_Redo(object? obj)
        {
            service.Redo();
        }


        internal bool CanExecute_FindText(object? obj)
        {
            return obj is Tuple<string, bool, bool> tuple && !string.IsNullOrEmpty(tuple.Item1);
        }
        internal void Execute_FindText(object? obj)
        {
            if (obj is Tuple<string, bool, bool> tuple)
                service.FindText(tuple.Item1, tuple.Item2, tuple.Item3);
        }


        internal bool CanExecute_ReplaceText(object? obj)
        {
            return obj is Tuple<string, string, bool, bool> tuple && !string.IsNullOrEmpty(tuple.Item1) && !string.IsNullOrEmpty(tuple.Item2);
        }
        internal void Execute_ReplaceText(object? obj)
        {
            if (obj is Tuple<string, string, bool, bool> tuple)
                service.ReplaceText(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
        }


        internal bool CanExecute_ReplaceAllText(object? obj)
        {
            return obj is Tuple<string, string, bool, bool> tuple && !string.IsNullOrEmpty(tuple.Item1) && !string.IsNullOrEmpty(tuple.Item2);
        }
        internal void Execute_ReplaceAllText(object? obj)
        {
            if (obj is Tuple<string, string, bool, bool> tuple)
                service.ReplaceAllText(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
        }


        internal bool CanExecute_ApplyBulletedList(object? obj)
        {
            return true;
        }
        internal void Execute_ApplyBulletedList(object? obj)
        {
            service.ApplyBulletedList();
        }


        internal bool CanExecute_ApplyNumberedList(object? obj)
        {
            return true;
        }
        internal void Execute_ApplyNumberedList(object? obj)
        {
            service.ApplyNumberedList();
        }


        internal bool CanExecute_RemoveListFormatting(object? obj)
        {
            return true;
        }
        internal void Execute_RemoveListFormatting(object? obj)
        {
            service.RemoveListFormatting();
        }


        internal bool CanExecute_IncreaseIndent(object? obj)
        {
            return true;
        }
        internal void Execute_IncreaseIndent(object? obj)
        {
            service.IncreaseIndent();
        }


        internal bool CanExecute_DecreaseIndent(object? obj)
        {
            return true;
        }
        internal void Execute_DecreaseIndent(object? obj)
        {
            service.DecreaseIndent();
        }


        internal bool CanExecute_Focus(object? obj)
        {
            return true;
        }
        internal void Execute_Focus(object? obj)
        {
            service.Focus();
        }


        internal bool CanExecute_InsertTextAtCaret(object? obj)
        {
            return obj is string text && !string.IsNullOrEmpty(text);
        }
        internal void Execute_InsertTextAtCaret(object? obj)
        {
            if (obj is string text)
                service.InsertTextAtCaret(text);
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
