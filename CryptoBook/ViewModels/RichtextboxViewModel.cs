using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class RichtextboxViewModel: ViewModelBase, IRichtextboxViewModel
    {
        private readonly RichtextboxModel richtextboxModel;
        public FlowDocument Document { get => richtextboxModel.Document;}
        public bool IsBold => richtextboxModel.IsBold;
        public bool IsItalic => richtextboxModel.IsItalic; 
        public bool IsUnderline => richtextboxModel.IsUnderlined; 
        public double FontSize => richtextboxModel.FontSize; 
        public string FontFamily => richtextboxModel.FontFamily;

        /// <summary>
        /// Конструктор ViewModel.
        /// </summary>
        /// <param name="scope">Область жизни для внедрения зависимостей.</param>
        public RichtextboxViewModel(ILifetimeScope scope)
        {
            richtextboxModel = new(scope);
            richtextboxModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand BoldCommand => boldCommand ??= new RelayCommand(richtextboxModel.Execute_BoldCommand, richtextboxModel.CanExecute_BoldCommand);
        RelayCommand boldCommand;

        public ICommand ItalicCommand => italicCommand??=new RelayCommand(richtextboxModel.Execute_ItalicCommand, richtextboxModel.CanExecute_ItalicCommand);
        RelayCommand italicCommand;

        public ICommand UnderlineCommand => underlineCommand??=new RelayCommand(richtextboxModel.Execute_UnderlineCommand, richtextboxModel.CanExecute_UnderlineCommand);
        RelayCommand underlineCommand;

        public ICommand ClearFormattingCommand => clearFormattingCommand??=new RelayCommand(richtextboxModel.Execute_ClearFormattingCommand, richtextboxModel.CanExecute_ClearFormattingCommand);
        RelayCommand clearFormattingCommand;

        public ICommand InsertTextCommand => insertTextCommand??=new RelayCommand(richtextboxModel.Execute_InsertTextCommand, richtextboxModel.CanExecute_InsertTextCommand);
        RelayCommand insertTextCommand;

        public ICommand ClearTextCommand => clearTextCommand??=new RelayCommand(richtextboxModel.Execute_ClearTextCommand, richtextboxModel.CanExecute_ClearTextCommand);
        RelayCommand clearTextCommand;

        public ICommand InsertImageCommand => insertImageCommand ??= new RelayCommand(richtextboxModel.Execute_InsertImageCommand, richtextboxModel.CanExecute_InsertImageCommand);
        RelayCommand insertImageCommand;

        public ICommand CopyCommand => copyCommand ??= new RelayCommand(richtextboxModel.Execute_CopyCommand, richtextboxModel.CanExecute_CopyCommand);
        RelayCommand copyCommand;

        public ICommand CutCommand => cutCommand ??= new RelayCommand(richtextboxModel.Execute_CutCommand, richtextboxModel.CanExecute_CutCommand);
        RelayCommand cutCommand;

        public ICommand PasteCommand => pasteCommand??=new RelayCommand(richtextboxModel.Execute_PasteCommand, richtextboxModel.CanExecute_PasteCommand);
        RelayCommand pasteCommand;


        public ICommand ChangeFontSizeCommand => changeFontSizeCommand??=new RelayCommand(richtextboxModel.Execute_ChangeFontSizeCommand, richtextboxModel.CanExecute_ChangeFontSizeCommand);
        RelayCommand changeFontSizeCommand;

        public ICommand ApplyTextAlignment => applyTextAlignment ??= new RelayCommand(richtextboxModel.Execute_ApplyTextAlignment, richtextboxModel.CanExecute_ApplyTextAlignment);
        RelayCommand applyTextAlignment;


        public ICommand ApplyAcceptsTab => applyAcceptsTab ??= new RelayCommand(richtextboxModel.Execute_ApplyAcceptsTab, richtextboxModel.CanExecute_ApplyAcceptsTab);
        RelayCommand applyAcceptsTab;


        public ICommand ApplyAcceptsReturn => new RelayCommand(richtextboxModel.Execute_ApplyAcceptsReturn, richtextboxModel.CanExecute_ApplyAcceptsReturn);
        RelayCommand applyAcceptsReturn;

        public ICommand ApplyVerticalScrollBarVisibility => applyVerticalScrollBarVisibility ??= new RelayCommand(richtextboxModel.Execute_ApplyVerticalScrollBarVisibility, richtextboxModel.CanExecute_ApplyVerticalScrollBarVisibility);
        RelayCommand applyVerticalScrollBarVisibility;


        public ICommand ApplyHorizontalScrollBarVisibility => applyHorizontalScrollBarVisibility ??= new RelayCommand(richtextboxModel.Execute_ApplyHorizontalScrollBarVisibility, richtextboxModel.CanExecute_ApplyHorizontalScrollBarVisibility);
        RelayCommand applyHorizontalScrollBarVisibility;

        public ICommand ApplyContextMenu => applyContextMenu ??= new RelayCommand(richtextboxModel.Execute_ApplyContextMenu, richtextboxModel.CanExecute_ApplyContextMenu);
        RelayCommand applyContextMenu;

        public ICommand ClearFormatting => clearFormatting ??= new RelayCommand(richtextboxModel.Execute_ClearFormatting, richtextboxModel.CanExecute_ClearFormatting);
        RelayCommand clearFormatting;

        public ICommand SelectAll => selectAll ??= new RelayCommand(richtextboxModel.Execute_SelectAll, richtextboxModel.CanExecute_SelectAll);
        RelayCommand selectAll;


        public ICommand ClearSelection => clearSelection ??= new RelayCommand(richtextboxModel.Execute_ClearSelection, richtextboxModel.CanExecute_ClearSelection);
        RelayCommand clearSelection;

        public ICommand GetSelectedTextAsString => getSelectedTextAsString ??= new RelayCommand(richtextboxModel.Execute_GetSelectedTextAsString, richtextboxModel.CanExecute_GetSelectedTextAsString);
        RelayCommand getSelectedTextAsString;

        public ICommand ReplaceSelectedText => replaceSelectedText ??= new RelayCommand(richtextboxModel.Execute_ReplaceSelectedText, richtextboxModel.CanExecute_ReplaceSelectedText);
        RelayCommand replaceSelectedText;

        public ICommand InsertHyperlink => insertHyperlink ??= new RelayCommand(richtextboxModel.Execute_InsertHyperlink, richtextboxModel.CanExecute_InsertHyperlink);
        RelayCommand insertHyperlink;

        public ICommand InsertParagraph => insertParagraph ??= new RelayCommand(richtextboxModel.Execute_InsertParagraph, richtextboxModel.CanExecute_InsertParagraph);
        RelayCommand insertParagraph;

        public ICommand InsertLineBreak => insertLineBreak ??= new RelayCommand(richtextboxModel.Execute_InsertLineBreak, richtextboxModel.CanExecute_InsertLineBreak);
        RelayCommand insertLineBreak;

        public ICommand InsertTable => insertTable ??= new RelayCommand(richtextboxModel.Execute_InsertTable, richtextboxModel.CanExecute_InsertTable);
        RelayCommand insertTable;

        public ICommand GetRtf => getRtf ??= new RelayCommand(richtextboxModel.Execute_GetRtf, richtextboxModel.CanExecute_GetRtf);
        RelayCommand getRtf;


        public ICommand LoadRtf => loadRtf ??= new RelayCommand(richtextboxModel.Execute_LoadRtf, richtextboxModel.CanExecute_LoadRtf);
        RelayCommand loadRtf;


        public ICommand LoadPlainText => loadPlainText ??= new RelayCommand(richtextboxModel.Execute_LoadPlainText, richtextboxModel.CanExecute_LoadPlainText);
        RelayCommand loadPlainText;

        public ICommand ClearDocument => clearDocument ??= new RelayCommand(richtextboxModel.Execute_ClearDocument, richtextboxModel.CanExecute_ClearDocument);
        RelayCommand clearDocument;

        public ICommand ScrollToCaret => scrollToCaret ??= new RelayCommand(richtextboxModel.Execute_ScrollToCaret, richtextboxModel.CanExecute_ScrollToCaret);
        RelayCommand scrollToCaret;

        public ICommand ScrollToEnd => scrollToEnd ??= new RelayCommand(richtextboxModel.Execute_ScrollToEnd, richtextboxModel.CanExecute_ScrollToEnd);
        RelayCommand scrollToEnd;

        public ICommand ScrollToStart => scrollToStart ??= new RelayCommand(richtextboxModel.Execute_ScrollToStart, richtextboxModel.CanExecute_ScrollToStart);
        RelayCommand scrollToStart;

        public ICommand SetDocumentMargin => setDocumentMargin ??= new RelayCommand(richtextboxModel.Execute_SetDocumentMargin, richtextboxModel.CanExecute_SetDocumentMargin);
        RelayCommand setDocumentMargin;

        public ICommand Undo => undo ??= new RelayCommand(richtextboxModel.Execute_Undo, richtextboxModel.CanExecute_Undo);
        RelayCommand undo;

        public ICommand Redo => redo ??= new RelayCommand(richtextboxModel.Execute_Redo, richtextboxModel.CanExecute_Redo);
        RelayCommand redo;

        public ICommand FindText => findText ??= new RelayCommand(richtextboxModel.Execute_FindText, richtextboxModel.CanExecute_FindText);
        RelayCommand findText;

        public ICommand ReplaceText => replaceText ??= new RelayCommand(richtextboxModel.Execute_ReplaceText, richtextboxModel.CanExecute_ReplaceText);
        RelayCommand replaceText;

        public ICommand ReplaceAllText => replaceAllText ??= new RelayCommand(richtextboxModel.Execute_ReplaceAllText, richtextboxModel.CanExecute_ReplaceAllText);
        RelayCommand replaceAllText;

        public ICommand ApplyBulletedList => applyBulletedList ??= new RelayCommand(richtextboxModel.Execute_ApplyBulletedList, richtextboxModel.CanExecute_ApplyBulletedList);
        RelayCommand applyBulletedList;

        public ICommand ApplyNumberedList => applyNumberedList ??= new RelayCommand(richtextboxModel.Execute_ApplyNumberedList, richtextboxModel.CanExecute_ApplyNumberedList);
        RelayCommand applyNumberedList;

        public ICommand RemoveListFormatting => removeListFormatting ??= new RelayCommand(richtextboxModel.Execute_RemoveListFormatting, richtextboxModel.CanExecute_RemoveListFormatting);
        RelayCommand removeListFormatting;

        public ICommand IncreaseIndent => increaseIndent ??= new RelayCommand(richtextboxModel.Execute_IncreaseIndent, richtextboxModel.CanExecute_IncreaseIndent);
        RelayCommand increaseIndent;

        public ICommand DecreaseIndent => decreaseIndent ??= new RelayCommand(richtextboxModel.Execute_DecreaseIndent, richtextboxModel.CanExecute_DecreaseIndent);
        RelayCommand decreaseIndent;

        public ICommand Focus => focus ??= new RelayCommand(richtextboxModel.Execute_Focus, richtextboxModel.CanExecute_Focus);
        RelayCommand focus;

        public ICommand InsertTextAtCaret => insertTextAtCaret ??= new RelayCommand(richtextboxModel.Execute_InsertTextAtCaret, richtextboxModel.CanExecute_InsertTextAtCaret);
        RelayCommand insertTextAtCaret;


        public ICommand Loaded => loaded ??= new RelayCommand(richtextboxModel.Execute_Loaded, richtextboxModel.CanExecute_Loaded);
        RelayCommand loaded;


        public ICommand Close => close ??= new RelayCommand(richtextboxModel.Execute_Close, richtextboxModel.CanExecute_Close);
        RelayCommand close;


        public ICommand Closing => closing ??= new RelayCommand(richtextboxModel.Execute_Closing, richtextboxModel.CanExecute_Closing);
        RelayCommand closing;


        public ICommand Closed => closed ??= new RelayCommand(richtextboxModel.Execute_Closed, richtextboxModel.CanExecute_Closed);
        RelayCommand closed;

        public ICommand ChangeFontFamilyCommand => new RelayCommand(richtextboxModel.Execute_ChangeFontFamilyCommand, richtextboxModel.CanExecute_ChangeFontFamilyCommand);
        RelayCommand changeFontFamilyCommand;

        public ICommand ChangeForegroundColor =>changeForegroundColor??= new RelayCommand(richtextboxModel.Execute_ChangeForegroundColor, richtextboxModel.CanExecute_ChangeForegroundColor);
        RelayCommand changeForegroundColor;

        public ICommand ChangeBackgroundColor => changeBackgroundColor ??= new RelayCommand(richtextboxModel.Execute_ChangeBackgroundColor, richtextboxModel.CanExecute_ChangeBackgroundColor);
        RelayCommand changeBackgroundColor;

    }
}
