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

        public void ApplyTextAlignment(TextAlignment alignment)
        {
            throw new NotImplementedException();
        }

        public void ApplyTextFormattingMode(TextFormattingMode mode)
        {
            throw new NotImplementedException();
        }

        public void ApplyTextRenderingMode(TextRenderingMode mode)
        {
            throw new NotImplementedException();
        }

        public void ApplyAcceptsTab(bool accept)
        {
            throw new NotImplementedException();
        }

        public void ApplyAcceptsReturn(bool accept)
        {
            throw new NotImplementedException();
        }

        public void ApplyVerticalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            throw new NotImplementedException();
        }

        public void ApplyHorizontalScrollBarVisibility(ScrollBarVisibility visibility)
        {
            throw new NotImplementedException();
        }

        public void ApplyContextMenu(ContextMenu menu)
        {
            throw new NotImplementedException();
        }

        public void ApplyDocumentEnabled(bool enabled)
        {
            throw new NotImplementedException();
        }

        public void ClearFormatting()
        {
            throw new NotImplementedException();
        }

        public void SelectAll()
        {
            throw new NotImplementedException();
        }

        public void ClearSelection()
        {
            throw new NotImplementedException();
        }

        public string GetSelectedTextAsString()
        {
            throw new NotImplementedException();
        }

        public void ReplaceSelectedText(string text)
        {
            throw new NotImplementedException();
        }

        public void InsertHyperlink(string uri, string displayText)
        {
            throw new NotImplementedException();
        }

        public void InsertImage(string imagePath, double width, double height)
        {
            throw new NotImplementedException();
        }

        public void InsertParagraph()
        {
            throw new NotImplementedException();
        }

        public void InsertLineBreak()
        {
            throw new NotImplementedException();
        }

        public void InsertTable(int rows, int columns)
        {
            throw new NotImplementedException();
        }

        public string GetRtf()
        {
            throw new NotImplementedException();
        }

        public void LoadRtf(string rtf)
        {
            throw new NotImplementedException();
        }

        public string GetPlainText()
        {
            throw new NotImplementedException();
        }

        public void LoadPlainText(string text)
        {
            throw new NotImplementedException();
        }

        public void ClearDocument()
        {
            throw new NotImplementedException();
        }

        public void ScrollToCaret()
        {
            throw new NotImplementedException();
        }

        public void ScrollToEnd()
        {
            throw new NotImplementedException();
        }

        public void ScrollToStart()
        {
            throw new NotImplementedException();
        }

        public void SetDocumentMargin(Thickness margin)
        {
            throw new NotImplementedException();
        }

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
