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
    public class TextFormatBarViewModel:ViewModelBase,ITextFormatBarViewModel
    {
        private readonly TextFormatBarModel model;

        public bool CanUndo => throw new NotImplementedException();

        public bool CanRedo => throw new NotImplementedException();

        public TextRange GetSelectedTextRange => throw new NotImplementedException();


        public TextFormatBarViewModel(IRichTextBoxService richTextBoxService, ITextFormatService formatService)
        {
            model= new TextFormatBarModel(richTextBoxService, formatService );
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }




        public ICommand SetTextAlignment => setTextAlignment ??= new RelayCommand(model.Execute_SetTextAlignment, model.CanExecute_SetTextAlignment);
        RelayCommand setTextAlignment;

        public ICommand SetParagraphIndent => setParagraphIndent ??=
            new RelayCommand(model.Execute_SetParagraphIndent, model.CanExecute_SetParagraphIndent);
        RelayCommand setParagraphIndent;

        public ICommand SetLineHeight => throw new NotImplementedException();

        public ICommand SetLineSpacing => throw new NotImplementedException();

        public ICommand ToggleBulletList => throw new NotImplementedException();

        public ICommand ToggleNumberedList => throw new NotImplementedException();

        public ICommand InsertHyperlink => throw new NotImplementedException();

        public ICommand InsertImage => throw new NotImplementedException();

        public ICommand ClearAllFormatting => throw new NotImplementedException();


        public ICommand ReplaceSelectedText => throw new NotImplementedException();

        public ICommand Undo => throw new NotImplementedException();

        public ICommand Redo => throw new NotImplementedException();


        public ICommand MoveCaretToStart => throw new NotImplementedException();

        public ICommand MoveCaretToEnd => throw new NotImplementedException();


        // IViewModel implementation

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();
    }
}
