using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace CryptoBook.ViewModels
{
    public class TitleBarRB_ViewModel: ViewModelBase, ITitleBarRB_ViewModel
    {
        private readonly ILifetimeScope scope;
        private readonly TitleBarRB_Model titleBarRB_Model;

        public bool CanUndo => titleBarRB_Model.CanUndo;

        public bool CanRedo => titleBarRB_Model.CanRedo;

        public ObservableCollection<double> FontSizes => titleBarRB_Model.FontSizes;
        public ObservableCollection<System.Windows.Media.FontFamily> FontFamilyes => titleBarRB_Model.FontFamilyes;



        public TitleBarRB_ViewModel(ILifetimeScope scope)
        {
            this.scope = scope;
            titleBarRB_Model = new TitleBarRB_Model(scope);
            titleBarRB_Model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }



        public ICommand BoldCommand => boldCommand ??= new RelayCommand(titleBarRB_Model.Execute_BoldCommand, titleBarRB_Model.CanExecute_BoldCommand);
        RelayCommand boldCommand;

        public ICommand ItalicCommand => italicCommand ??= new RelayCommand(titleBarRB_Model.Execute_ItalicCommand, titleBarRB_Model.CanExecute_ItalicCommand);
        RelayCommand italicCommand;

        public ICommand UnderlineCommand => underlineCommand ??= new RelayCommand(titleBarRB_Model.Execute_UnderlineCommand, titleBarRB_Model.CanExecute_UnderlineCommand);
        RelayCommand underlineCommand;

        public ICommand ClearFormattingCommand => clearFormattingCommand ??= new RelayCommand(titleBarRB_Model.Execute_ClearFormattingCommand, titleBarRB_Model.CanExecute_ClearFormattingCommand);
        RelayCommand clearFormattingCommand;

        public ICommand InsertImageCommand => insertImageCommand ??= new RelayCommand(titleBarRB_Model.Execute_InsertImageCommand, titleBarRB_Model.CanExecute_InsertImageCommand);
        RelayCommand insertImageCommand;

        public ICommand ApplyFontSize => applyFontSize ??= new RelayCommand(titleBarRB_Model.Execute_ApplyFontSize, titleBarRB_Model.CanExecute_ApplyFontSize);
        RelayCommand applyFontSize;

        public ICommand ApplyFontFamily => applyFontFamily ??= new RelayCommand(titleBarRB_Model.Execute_ApplyFontFamily, titleBarRB_Model.CanExecute_ApplyFontFamily);
        RelayCommand applyFontFamily;

        public ICommand ApplyForegroundColor => applyForegroundColor ??= new RelayCommand(titleBarRB_Model.Execute_ApplyForegroundColor, titleBarRB_Model.CanExecute_ApplyForegroundColor);
        RelayCommand applyForegroundColor;

        public ICommand ApplyBackgroundColor => applyBackgroundColor ??= new RelayCommand(titleBarRB_Model.Execute_ApplyBackgroundColor, titleBarRB_Model.CanExecute_ApplyBackgroundColor);
        RelayCommand applyBackgroundColor;

        public ICommand ApplyTextAlignment => applyTextAlignment ??= new RelayCommand(titleBarRB_Model.Execute_ApplyTextAlignment, titleBarRB_Model.CanExecute_ApplyTextAlignment);
        RelayCommand applyTextAlignment;

        public ICommand ApplyTextFormattingMode => applyTextFormattingMode ??= new RelayCommand(titleBarRB_Model.Execute_ApplyTextFormattingMode, titleBarRB_Model.CanExecute_ApplyTextFormattingMode);
        RelayCommand applyTextFormattingMode;

        public ICommand ApplyTextRenderingMode => applyTextRenderingMode ??= new RelayCommand(titleBarRB_Model.Execute_ApplyTextRenderingMode, titleBarRB_Model.CanExecute_ApplyTextRenderingMode);
        RelayCommand applyTextRenderingMode;

        public ICommand ApplyAcceptsTab => applyAcceptsTab ??= new RelayCommand(titleBarRB_Model.Execute_ApplyAcceptsTab, titleBarRB_Model.CanExecute_ApplyAcceptsTab);
        RelayCommand applyAcceptsTab;

        public ICommand ApplyAcceptsReturn => applyAcceptsReturn ??= new RelayCommand(titleBarRB_Model.Execute_ApplyAcceptsReturn, titleBarRB_Model.CanExecute_ApplyAcceptsReturn);
        RelayCommand applyAcceptsReturn;

        public ICommand ApplyVerticalScrollBarVisibility => applyVerticalScrollBarVisibility ??= new RelayCommand(titleBarRB_Model.Execute_ApplyVerticalScrollBarVisibility, titleBarRB_Model.CanExecute_ApplyVerticalScrollBarVisibility);
        RelayCommand applyVerticalScrollBarVisibility;

        public ICommand ApplyHorizontalScrollBarVisibility => new RelayCommand(titleBarRB_Model.Execute_ApplyHorizontalScrollBarVisibility, titleBarRB_Model.CanExecute_ApplyHorizontalScrollBarVisibility);


        public ICommand ClearFormatting => clearFormatting ??= new RelayCommand(titleBarRB_Model.Execute_ClearFormatting, titleBarRB_Model.CanExecute_ClearFormatting);
        RelayCommand clearFormatting;

        public ICommand SelectAll => selectAll ??= new RelayCommand(titleBarRB_Model.Execute_SelectAll, titleBarRB_Model.CanExecute_SelectAll);
        RelayCommand selectAll;


        public ICommand ClearSelection => clearSelection ??= new RelayCommand(titleBarRB_Model.Execute_ClearSelection, titleBarRB_Model.CanExecute_ClearSelection);
        RelayCommand clearSelection;


        public ICommand ReplaceSelectedText => replaceSelectedText ??= new RelayCommand(titleBarRB_Model.Execute_ReplaceSelectedText, titleBarRB_Model.CanExecute_ReplaceSelectedText);
        RelayCommand replaceSelectedText;

        public ICommand InsertHyperlink => insertHyperlink ??= new RelayCommand(titleBarRB_Model.Execute_InsertHyperlink, titleBarRB_Model.CanExecute_InsertHyperlink);
        RelayCommand insertHyperlink;


        public ICommand InsertParagraph => insertParagraph ??= new RelayCommand(titleBarRB_Model.Execute_InsertParagraph, titleBarRB_Model.CanExecute_InsertParagraph);
        RelayCommand insertParagraph;

        public ICommand InsertLineBreak => insertLineBreak ??= new RelayCommand(titleBarRB_Model.Execute_InsertLineBreak, titleBarRB_Model.CanExecute_InsertLineBreak);
        RelayCommand insertLineBreak;

        public ICommand InsertTable => insertTable ??= new RelayCommand(titleBarRB_Model.Execute_InsertTable, titleBarRB_Model.CanExecute_InsertTable);
        RelayCommand insertTable;


        public ICommand ClearDocument => clearDocument ??= new RelayCommand(titleBarRB_Model.Execute_ClearDocument, titleBarRB_Model.CanExecute_ClearDocument);
        RelayCommand clearDocument;


        public ICommand ScrollToCaret => scrollToCaret ??= new RelayCommand(titleBarRB_Model.Execute_ScrollToCaret, titleBarRB_Model.CanExecute_ScrollToCaret);
        RelayCommand scrollToCaret;

        public ICommand ScrollToEnd => scrollToEnd ??= new RelayCommand(titleBarRB_Model.Execute_ScrollToEnd, titleBarRB_Model.CanExecute_ScrollToEnd);
        RelayCommand scrollToEnd;

        public ICommand ScrollToStart => scrollToStart ??= new RelayCommand(titleBarRB_Model.Execute_ScrollToStart, titleBarRB_Model.CanExecute_ScrollToStart);
        RelayCommand scrollToStart;

        public ICommand SetDocumentMargin => setDocumentMargin ??= new RelayCommand(titleBarRB_Model.Execute_SetDocumentMargin, titleBarRB_Model.CanExecute_SetDocumentMargin);
        RelayCommand setDocumentMargin;

        public void Undo()
        {
            throw new NotImplementedException();
        }

        public void Redo()
        {
            throw new NotImplementedException();
        }

        public bool FindText(string searchText, bool matchCase = false, bool wholeWord = false)
        {
            throw new NotImplementedException();
        }

        public void ReplaceText(string searchText, string replaceText, bool matchCase = false, bool wholeWord = false)
        {
            throw new NotImplementedException();
        }

        public void ReplaceAllText(string searchText, string replaceText, bool matchCase = false, bool wholeWord = false)
        {
            throw new NotImplementedException();
        }

        public void ApplyBulletedList()
        {
            throw new NotImplementedException();
        }

        public void ApplyNumberedList()
        {
            throw new NotImplementedException();
        }

        public void RemoveListFormatting()
        {
            throw new NotImplementedException();
        }

        public void IncreaseIndent()
        {
            throw new NotImplementedException();
        }

        public void DecreaseIndent()
        {
            throw new NotImplementedException();
        }

        public void Focus()
        {
            throw new NotImplementedException();
        }

        public void InsertTextAtCaret(string text)
        {
            throw new NotImplementedException();
        }

        public ICommand Loaded => loaded ??= new RelayCommand(titleBarRB_Model.Execute_Loaded, titleBarRB_Model.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(titleBarRB_Model.Execute_Close, titleBarRB_Model.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(titleBarRB_Model.Execute_Closing, titleBarRB_Model.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Closed => closed ??= new RelayCommand(titleBarRB_Model.Execute_Closed, titleBarRB_Model.CanExecute_Closed);
        RelayCommand closed;

    }
}
