using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Windows;

namespace CryptoBook.Models
{
    internal class TitleBarRB_Model: ViewModelBase
    {

        private readonly ILifetimeScope scope;
        private readonly IRichTextBoxService service;
        private readonly IRichtextboxViewModel richtextbox;

        public ObservableCollection<double> FontSizes { get=>fontSizes; internal set=>SetProperty(ref fontSizes,value); }
        public bool CanUndo { get; internal set; }
        public bool CanRedo { get; internal set; }
        public ObservableCollection<System.Windows.Media.FontFamily> FontFamilyes { get; internal set; }

        private ObservableCollection<double> fontSizes;

        public TitleBarRB_Model(ILifetimeScope _scope)
        {
            scope = _scope;
            service = scope.Resolve<IRichTextBoxService>();
            richtextbox = scope.Resolve<IRichtextboxViewModel>();
            FontSizes = new ObservableCollection<double>();
        }


        internal bool CanExecute_BoldCommand(object? obj)
        {
            return richtextbox.BoldCommand.CanExecute(null);
        }
        internal void Execute_BoldCommand(object? obj)
        {
            richtextbox.BoldCommand.Execute(obj);
        }


        internal bool CanExecute_ItalicCommand(object? obj)
        {
            return richtextbox.ItalicCommand.CanExecute(obj);
        }
        internal void Execute_ItalicCommand(object? obj)
        {
            richtextbox.ItalicCommand.Execute(obj);
        }


        internal bool CanExecute_UnderlineCommand(object? obj)
        {
            return richtextbox.UnderlineCommand.CanExecute(obj);
        }
        internal void Execute_UnderlineCommand(object? obj)
        {
            richtextbox.UnderlineCommand.Execute(obj);
        }


        internal bool CanExecute_ClearFormattingCommand(object? obj)
        {
            return richtextbox.ClearFormattingCommand.CanExecute(obj);
        }
        internal void Execute_ClearFormattingCommand(object? obj)
        {
            richtextbox.ClearFormattingCommand.Execute(obj);
        }


        internal bool CanExecute_InsertImageCommand(object? obj)
        {
            return richtextbox.InsertImageCommand.CanExecute(obj);
        }
        internal void Execute_InsertImageCommand(object? obj)
        {
            richtextbox.InsertImageCommand.Execute(obj);
        }


        internal bool CanExecute_ApplyFontSize(object? obj)
        {
            return richtextbox.ChangeFontSizeCommand.CanExecute(obj);
        }
        internal void Execute_ApplyFontSize(object? obj)
        {
            richtextbox.ChangeFontSizeCommand.Execute(obj);
        }


        internal bool CanExecute_ApplyFontFamily(object? obj)
        {
            return richtextbox.ChangeFontFamilyCommand.CanExecute(obj);
        }
        internal void Execute_ApplyFontFamily(object? obj)
        {
            richtextbox.ChangeFontFamilyCommand.Execute(obj);
        }

        internal bool CanExecute_ApplyForegroundColor(object? obj)
        {
            return richtextbox.ChangeForegroundColor.CanExecute(obj);
        }
        internal void Execute_ApplyForegroundColor(object? obj)
        {
            richtextbox.ChangeForegroundColor.Execute(obj);
        }


        internal bool CanExecute_ApplyBackgroundColor(object? obj)
        {
            return richtextbox.ChangeBackgroundColor.CanExecute(obj);
        }
        internal void Execute_ApplyBackgroundColor(object? obj)
        {
            richtextbox.ChangeBackgroundColor.Execute(obj);
        }


        internal bool CanExecute_ApplyTextAlignment(object? obj)
        {
            return richtextbox.ApplyTextAlignment.CanExecute(obj);
        }
        internal void Execute_ApplyTextAlignment(object? obj)
        {
            richtextbox.ApplyTextAlignment.Execute(obj);
        }


        internal bool CanExecute_ApplyTextFormattingMode(object? obj)
        {
            return richtextbox.ApplyTextFormattingMode.CanExecute(obj);
        }
        internal void Execute_ApplyTextFormattingMode(object? obj)
        {
            richtextbox.ApplyTextFormattingMode.Execute(obj);
        }


        internal bool CanExecute_ApplyTextRenderingMode(object? obj)
        {
            return richtextbox.ApplyTextRenderingMode.CanExecute (obj);
        }
        internal void Execute_ApplyTextRenderingMode(object? obj)
        {
            richtextbox.ApplyTextRenderingMode.Execute(obj);
        }


        internal bool CanExecute_ApplyAcceptsReturn(object? obj)
        {
            return richtextbox.ApplyAcceptsReturn.CanExecute(obj);
        }
        internal void Execute_ApplyAcceptsReturn(object? obj)
        {
            richtextbox.ApplyAcceptsReturn.Execute(obj);
        }

        internal bool CanExecute_ApplyAcceptsTab(object? obj)
        {
            return richtextbox.ApplyAcceptsTab.CanExecute(obj);
        }
        internal void Execute_ApplyAcceptsTab(object? obj)
        {
            richtextbox.ApplyAcceptsTab.Execute(obj);
        }


        internal bool CanExecute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            return richtextbox.ApplyVerticalScrollBarVisibility.CanExecute(obj);
        }
        internal void Execute_ApplyVerticalScrollBarVisibility(object? obj)
        {
            richtextbox.ApplyVerticalScrollBarVisibility.Execute(obj);
        }


        internal bool CanExecute_ApplyHorizontalScrollBarVisibility(object? obj)
        {
            return richtextbox.ApplyHorizontalScrollBarVisibility.CanExecute(obj);
        }
        internal void Execute_ApplyHorizontalScrollBarVisibility(object? obj)
        {
            richtextbox.ApplyHorizontalScrollBarVisibility.Execute(obj);
        }


        internal bool CanExecute_ClearFormatting(object? obj)
        {
            return richtextbox.ClearFormatting.CanExecute(obj);
        }
        internal void Execute_ClearFormatting(object? obj)
        {
            richtextbox.ClearFormatting.Execute(obj);
        }


        internal bool CanExecute_SelectAll(object? obj)
        {
            return richtextbox.SelectAll.CanExecute(obj);
        }
        internal void Execute_SelectAll(object? obj)
        {
            richtextbox.SelectAll.Execute(obj);
        }


        internal bool CanExecute_ClearSelection(object? obj)
        {
            return richtextbox.ClearSelection.CanExecute(obj);
        }
        internal void Execute_ClearSelection(object? obj)
        {
            richtextbox.ClearSelection.Execute(obj);
        }


        internal bool CanExecute_ReplaceSelectedText(object? obj)
        {
            return richtextbox.ReplaceSelectedText.CanExecute(obj);
        }
        internal void Execute_ReplaceSelectedText(object? obj)
        {
            richtextbox.ReplaceSelectedText.Execute(obj);
        }


        internal bool CanExecute_InsertHyperlink(object? obj)
        {
            return richtextbox.InsertHyperlink.CanExecute(obj);
        }
        internal void Execute_InsertHyperlink(object? obj)
        {
            richtextbox.InsertHyperlink.Execute(obj);
        }


        internal bool CanExecute_InsertParagraph(object? obj)
        {
            return richtextbox.InsertParagraph.CanExecute(obj);
        }
        internal void Execute_InsertParagraph(object? obj)
        {
            richtextbox.InsertParagraph.Execute(obj);
        }


        internal bool CanExecute_InsertLineBreak(object? obj)
        {
            return richtextbox.InsertLineBreak.CanExecute(obj);
        }
        internal void Execute_InsertLineBreak(object? obj)
        {
            richtextbox.InsertLineBreak.Execute(obj);
        }


        internal bool CanExecute_InsertTable(object? obj)
        {
            return richtextbox.InsertTable.CanExecute(obj);
        }
        internal void Execute_InsertTable(object? obj)
        {
            richtextbox.InsertTable.Execute(obj);
        }


        internal bool CanExecute_ClearDocument(object? obj)
        {
            return richtextbox.ClearDocument.CanExecute(obj);
        }
        internal void Execute_ClearDocument(object? obj)
        {
            richtextbox.ClearDocument.Execute(obj);
        }


        internal bool CanExecute_ScrollToCaret(object? obj)
        {
            return richtextbox.ScrollToCaret.CanExecute(obj);
        }
        internal void Execute_ScrollToCaret(object? obj)
        {
            richtextbox.ScrollToCaret.Execute(obj);
        }


        internal bool CanExecute_ScrollToEnd(object? obj)
        {
            return richtextbox.ScrollToEnd.CanExecute(obj);
        }
        internal void Execute_ScrollToEnd(object? obj)
        {
            richtextbox.ScrollToEnd.Execute(obj);
        }


        internal bool CanExecute_ScrollToStart(object? obj)
        {
            return richtextbox.ScrollToStart.CanExecute(obj);
        }
        internal void Execute_ScrollToStart(object? obj)
        {
            richtextbox.ScrollToStart.Execute(obj);
        }


        internal bool CanExecute_SetDocumentMargin(object? obj)
        {
            return richtextbox.SetDocumentMargin.CanExecute(obj);
        }
        internal void Execute_SetDocumentMargin(object? obj)
        {
            richtextbox.SetDocumentMargin.Execute(obj);
        }


        internal bool CanExecute_Undo(object? obj)
        {
            return richtextbox.Undo.CanExecute(obj);
        }
        internal void Execute_Undo(object? obj)
        {
            richtextbox.Undo.Execute(obj);
        }


        internal bool CanExecute_Redo(object? obj)
        {
            return richtextbox.Redo.CanExecute(obj);
        }
        internal void Execute_Redo(object? obj)
        {
            richtextbox.Redo.Execute(obj);
        }


        internal bool CanExecute_FindText(object? obj)
        {
            return richtextbox.FindText.CanExecute(obj);
        }
        internal void Execute_FindText(object? obj)
        {
            richtextbox.FindText.Execute(obj);
        }


        internal bool CanExecute_ReplaceText(object? obj)
        {
            return richtextbox.ReplaceText.CanExecute(obj);
        }
        internal void Execute_ReplaceText(object? obj)
        {
            richtextbox.ReplaceText.Execute(obj);
        }


        internal bool CanExecute_ReplaceAllText(object? obj)
        {
            return richtextbox.ReplaceAllText.CanExecute(obj);
        }
        internal void Execute_ReplaceAllText(object? obj)
        {
            richtextbox.ReplaceAllText.Execute(obj);
        }


        internal bool CanExecute_ApplyBulletedList(object? obj)
        {
            return richtextbox.ApplyBulletedList.CanExecute(obj);
        }
        internal void Execute_ApplyBulletedList(object? obj)
        {
            richtextbox.ApplyBulletedList.Execute(obj);
        }


        internal bool CanExecute_ApplyNumberedList(object? obj)
        {
            return richtextbox.ApplyNumberedList.CanExecute(obj);
        }
        internal void Execute_ApplyNumberedList(object? obj)
        {
            richtextbox.ApplyNumberedList.Execute(obj);
        }


        internal bool CanExecute_RemoveListFormatting(object? obj)
        {
            return richtextbox.RemoveListFormatting.CanExecute(obj);    
        }
        internal void Execute_RemoveListFormatting(object? obj)
        {
            richtextbox.RemoveListFormatting.Execute(obj);
        }



        internal bool CanExecute_IncreaseIndent(object? obj)
        {
            return richtextbox.IncreaseIndent.CanExecute(obj);
        }
        internal void Execute_IncreaseIndent(object? obj)
        {
            richtextbox.IncreaseIndent.Execute(obj);
        }


        internal bool CanExecute_DecreaseIndent(object? obj)
        {
            return richtextbox.DecreaseIndent.CanExecute(obj);
        }
        internal void Execute_DecreaseIndent(object? obj)
        {
            richtextbox.DecreaseIndent.Execute(obj);
        }


        internal bool CanExecute_Focus(object? obj)
        {
            return richtextbox.Focus.CanExecute(obj);
        }
        internal void Execute_Focus(object? obj)
        {
            richtextbox.Focus.Execute(obj);
        }


        internal bool CanExecute_InsertTextAtCaret(object? obj)
        {
            return richtextbox.InsertTextAtCaret.CanExecute(obj);
        }
        internal void Execute_InsertTextAtCaret(object? obj)
        {
            richtextbox.InsertTextAtCaret.Execute(obj);
        }




        internal bool CanExecute_Loaded(object? obj)
        {
            return service != null;
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
        }


        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
        {
        }


        internal bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        internal void Execute_Closed(object? obj)
        {
        }

    }
}
