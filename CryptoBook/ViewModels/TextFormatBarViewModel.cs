using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class TextFormatBarViewModel:ViewModelBase,ITextFormatBarViewModel
    {
        private readonly TextFormatBarModel model;

        public TextFormatBarViewModel(IRichTextBoxService richTextBoxService, ITextFormatService formatService)
        {
            model= new TextFormatBarModel(richTextBoxService, formatService );
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand SetTextAlignment => setTextAlignment ??= new RelayCommand(model.Execute_SetTextAlignment, model.CanExecute_SetTextAlignment);
        RelayCommand? setTextAlignment;

        public ICommand SetParagraphIndent => setParagraphIndent ??=
            new RelayCommand(model.Execute_SetParagraphIndent, model.CanExecute_SetParagraphIndent);
        RelayCommand? setParagraphIndent;

        public ICommand SetLineHeight => setLineHeight ??= new RelayCommand(model.Execute_SetLineHeight, model.CanExecute_SetLineHeight);
        RelayCommand? setLineHeight;


        // IViewModel implementation

        public ICommand Loaded => NoOpCommand;
        public ICommand Close => NoOpCommand;
        public ICommand Closing => NoOpCommand;
        public ICommand Closed => NoOpCommand;

        private static ICommand NoOpCommand { get; } = new RelayCommand(_ => { });

    }
}
