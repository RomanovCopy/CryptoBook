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


        /// <summary>
        /// Конструктор ViewModel.
        /// </summary>
        /// <param name="scope">Область жизни для внедрения зависимостей.</param>
        public RichtextboxViewModel(ILifetimeScope scope)
        {
            richtextboxModel = new(scope);
            richtextboxModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }




        public ICommand Loaded => loaded ??= new RelayCommand(richtextboxModel.Execute_Loaded, richtextboxModel.CanExecute_Loaded);
        RelayCommand loaded;


        public ICommand Close => close ??= new RelayCommand(richtextboxModel.Execute_Close, richtextboxModel.CanExecute_Close);
        RelayCommand close;


        public ICommand Closing => closing ??= new RelayCommand(richtextboxModel.Execute_Closing, richtextboxModel.CanExecute_Closing);
        RelayCommand closing;


        public ICommand Closed => closed ??= new RelayCommand(richtextboxModel.Execute_Closed, richtextboxModel.CanExecute_Closed);
        RelayCommand closed;

        public double FontSize => throw new NotImplementedException();

        public string FontFamily => throw new NotImplementedException();

        public Color FontColor => throw new NotImplementedException();

        public Color FontBackground => throw new NotImplementedException();

        public ICommand BoldCommand => throw new NotImplementedException();

        public ICommand ItalicCommand => throw new NotImplementedException();

        public ICommand UnderlineCommand => throw new NotImplementedException();

        public ICommand ClearFormattingCommand => throw new NotImplementedException();

        public ICommand InsertTextCommand => throw new NotImplementedException();

        public ICommand ClearTextCommand => throw new NotImplementedException();

        public ICommand InsertImageCommand => throw new NotImplementedException();

        public ICommand CopyCommand => throw new NotImplementedException();

        public ICommand CutCommand => throw new NotImplementedException();

        public ICommand PasteCommand => throw new NotImplementedException();

        public ICommand ChangeFontSizeCommand => throw new NotImplementedException();

        public ICommand ChangeFontFamilyCommand => throw new NotImplementedException();

        public ICommand ChangeForegroundColor => throw new NotImplementedException();

        public ICommand ChangeBackgroundColor => throw new NotImplementedException();

        public ICommand ApplyTextAlignment => throw new NotImplementedException();

        public ICommand ApplyAcceptsTab => throw new NotImplementedException();

        public ICommand ApplyAcceptsReturn => throw new NotImplementedException();

        public ICommand ApplyVerticalScrollBarVisibility => throw new NotImplementedException();

        public ICommand ApplyHorizontalScrollBarVisibility => throw new NotImplementedException();

        public ICommand ApplyContextMenu => throw new NotImplementedException();

        public ICommand ClearFormatting => throw new NotImplementedException();

        public ICommand SelectAll => throw new NotImplementedException();

        public ICommand ClearSelection => throw new NotImplementedException();

        public ICommand GetSelectedTextAsString => throw new NotImplementedException();

        public ICommand ReplaceSelectedText => throw new NotImplementedException();

        public ICommand InsertHyperlink => throw new NotImplementedException();

        public ICommand InsertParagraph => throw new NotImplementedException();

        public ICommand InsertLineBreak => throw new NotImplementedException();

        public ICommand InsertTable => throw new NotImplementedException();

        public ICommand GetRtf => throw new NotImplementedException();

        public ICommand LoadRtf => throw new NotImplementedException();

        public ICommand LoadPlainText => throw new NotImplementedException();

        public ICommand ClearDocument => throw new NotImplementedException();

        public ICommand ScrollToCaret => throw new NotImplementedException();

        public ICommand ScrollToEnd => throw new NotImplementedException();

        public ICommand ScrollToStart => throw new NotImplementedException();

        public ICommand SetDocumentMargin => throw new NotImplementedException();

        public ICommand Undo => throw new NotImplementedException();

        public ICommand Redo => throw new NotImplementedException();

        public ICommand FindText => throw new NotImplementedException();

        public ICommand ReplaceText => throw new NotImplementedException();

        public ICommand ReplaceAllText => throw new NotImplementedException();

        public ICommand ApplyBulletedList => throw new NotImplementedException();

        public ICommand ApplyNumberedList => throw new NotImplementedException();

        public ICommand RemoveListFormatting => throw new NotImplementedException();

        public ICommand IncreaseIndent => throw new NotImplementedException();

        public ICommand DecreaseIndent => throw new NotImplementedException();

        public ICommand Focus => throw new NotImplementedException();

        public ICommand InsertTextAtCaret => throw new NotImplementedException();
    }
}
