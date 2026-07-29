using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class RichTextContextMenuViewModel: ViewModelBase, IRichTextContextMenuViewModel
    {
        private readonly IRichTextBoxService richTextBox;

        public IFontFormatBar_ViewModel FontFormatting { get; }
        public ITextFormatBarViewModel TextFormatting { get; }
        public IListFormatBarViewModel ListFormatting { get; }

        public FontWeight Bold => FontWeights.Bold;
        public System.Windows.FontStyle Italic => FontStyles.Italic;
        public ITextDecorationItem? Underline =>
            FontFormatting.TextDecorations.FirstOrDefault(item => item.Name == "Underline");

        public ICommand ClearDocument => clearDocument ??=
            new RelayCommand(ExecuteClearDocument, CanExecuteClearDocument);
        private RelayCommand? clearDocument;

        public RichTextContextMenuViewModel( IRichTextBoxService richTextBox, IFontFormatBar_ViewModel fontFormatting,
            ITextFormatBarViewModel textFormatting, IListFormatBarViewModel listFormatting)
        {
            this.richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
            FontFormatting = fontFormatting ?? throw new ArgumentNullException(nameof(fontFormatting));
            TextFormatting = textFormatting ?? throw new ArgumentNullException(nameof(textFormatting));
            ListFormatting = listFormatting ?? throw new ArgumentNullException(nameof(listFormatting));
        }

        private bool CanExecuteClearDocument(object? parameter) =>
            !richTextBox.IsReadOnly && HasDocumentContent();

        private void ExecuteClearDocument(object? parameter) => richTextBox.ClearDocument();

        private bool HasDocumentContent()
        {
            var blocks = richTextBox.Document.Blocks.ToList();
            if(blocks.Count != 1 || blocks[0] is not Paragraph paragraph)
                return blocks.Count > 0;

            return paragraph.Inlines.Any(inline =>
                inline is not Run run || !string.IsNullOrEmpty(run.Text));
        }

        public ICommand Loaded => NoOpCommand;
        public ICommand Close => NoOpCommand;
        public ICommand Closing => NoOpCommand;
        public ICommand Closed => NoOpCommand;

        private static ICommand NoOpCommand { get; } = new RelayCommand(_ => { });
    }
}
