using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;

namespace CryptoBook.Models
{
    internal class FontFormatBar_Model: ViewModelBase
    {
        private readonly ILifetimeScope scope;
        private readonly IRichTextBoxService richTextBoxService;
        private readonly IFlowDocumentService flowDocumentService;

        public bool IsBold => richTextBoxService.IsBold;
        public bool IsItalic => richTextBoxService.IsItalic;
        public bool IsUnderline => richTextBoxService.IsUnderline;
        public double FontSize 
        {
            get => fontSize;
            set=>SetProperty(ref fontSize, value);
        }
        double fontSize;
        public string FontFamily => richTextBoxService.FontFamily;
        public string FontColor => richTextBoxService.FontColor;
        public string FontStile => richTextBoxService.FontStile;

        public ObservableCollection<double> FontSizes => richTextBoxService.FontSizes;
        public ObservableCollection<string> FontFamilies => richTextBoxService.FontFamilies;
        public ObservableCollection<Color> FontColors => richTextBoxService.FontColors;
        public ObservableCollection<Brush> BackgrondColor => richTextBoxService.BackgrondColor;


        internal FontFormatBar_Model(ILifetimeScope scope)
        {
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            richTextBoxService= scope.Resolve<IRichTextBoxService>() ?? throw new ArgumentNullException(nameof(IRichTextBoxService));
            flowDocumentService = scope.Resolve<IFlowDocumentService>() ?? throw new ArgumentNullException(nameof(IFlowDocumentService));
        }
        internal bool CanExecute_Bold(object? obj) 
        {
            return !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_Bold(object? obj) 
        {
            flowDocumentService.ToggleBold(richTextBoxService.Selection);
        }

        internal bool CanExecute_Italic(object? obj) 
        {
            return !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_Italic(object? obj) 
        {
            flowDocumentService.ToggleItalic(richTextBoxService.Selection);
        }

        internal bool CanExecute_Underline(object? obj) 
        {
            return !richTextBoxService.Selection.IsEmpty;
        }
        internal void Execute_Underline(object? obj) 
        {
            flowDocumentService.ToggleUnderline(richTextBoxService.Selection);
        }

        internal bool CanExecute_ClearFormatting(object? obj) { return true; }
        internal void Execute_ClearFormatting(object? obj) { }

        internal bool CanExecute_ChangeFontSize(object? obj) 
        { 
            return true; 
        }
        internal void Execute_ChangeFontSize(object? obj) 
        {
            if(obj is double fontSize)
            {
                flowDocumentService.ApplyFontSize(richTextBoxService.Selection, FontSize);
            }
        }

        internal bool CanExecute_ChangeFontFamily(object? obj) { return true; }
        internal void Execute_ChangeFontFamily(object? obj) { }

        internal bool CanExecute_ChangeTextAlignment(object? obj) { return true; }
        internal void Execute_ChangeTextAlignment(object? obj) { }

        internal bool CanExecute_ChangeForeground(object? obj) { return true; }
        internal void Execute_ChangeForeground(object? obj) { }

        internal bool CanExecute_ChangeBackground(object? obj) { return true; }
        internal void Execute_ChangeBackground(object? obj) { }

        internal bool CanExecute_Loaded(object? obj) { return true; }
        internal void OnLoaded(object? obj) { }

        internal bool CanExecute_Close(object? obj) { return true; }
        internal void OnClose(object? obj) { }

        internal bool CanExecute_Closing(object? obj) { return true; }
        internal void OnClosing(object? obj) { }

        internal bool CanExecute_Closed(object? obj) { return true; }
        internal void OnClosed(object? obj) { }
    }
}