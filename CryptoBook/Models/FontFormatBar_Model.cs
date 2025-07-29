using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Windows.Documents;

namespace CryptoBook.Models
{
    internal class FontFormatBar_Model: ViewModelBase
    {
        private readonly ILifetimeScope scope;
        private readonly IRichTextBoxService richService;
        private readonly IFlowDocumentService flowService;

        public bool IsBold => richService.IsBold;
        public bool IsItalic => richService.IsItalic;
        public bool IsUnderline => richService.IsUnderline;
        public double FontSize
        {
            get => fontSize;
            set =>SetProperty(ref fontSize, value);
        }
        double fontSize;
        public string FontFamily => richService.FontFamily;
        public string FontColor => richService.FontColor;
        public string FontStile => richService.FontStile;

        public ObservableCollection<double> FontSizes => richService.FontSizes;
        public ObservableCollection<string> FontFamilies => richService.FontFamilies;
        public ObservableCollection<Color> FontColors => richService.FontColors;
        public ObservableCollection<Brush> BackgrondColor => richService.BackgrondColor;


        internal FontFormatBar_Model(ILifetimeScope scope)
        {
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            richService = scope.Resolve<IRichTextBoxService>() ?? throw new ArgumentNullException(nameof(IRichTextBoxService));
            flowService = scope.Resolve<IFlowDocumentService>() ?? throw new ArgumentNullException(nameof(IFlowDocumentService));
            FontSize = 12.0;
        }
        internal bool CanExecute_Bold(object? obj)
        {
            return !richService.Selection.IsEmpty;
        }
        internal void Execute_Bold(object? obj)
        {
            flowService.ToggleBold(richService.Selection);
        }

        internal bool CanExecute_Italic(object? obj)
        {
            return !richService.Selection.IsEmpty;
        }
        internal void Execute_Italic(object? obj)
        {
            flowService.ToggleItalic(richService.Selection);
        }

        internal bool CanExecute_Underline(object? obj)
        {
            return !richService.Selection.IsEmpty;
        }
        internal void Execute_Underline(object? obj)
        {
            flowService.ToggleUnderline(richService.Selection);
        }

        internal bool CanExecute_ClearFormatting(object? obj) { return true; }
        internal void Execute_ClearFormatting(object? obj) { }

        internal bool CanExecute_ChangeFontSize(object? obj)
        {
            if(obj is double size)
            {
                return FontSizes.Any((x)=>x==size);
            }
            return false;
            ;
        }
        internal void Execute_ChangeFontSize(object? obj)
        {
            if(obj is double fontSize)
            {
                var selection = richService.Selection;
                flowService.ApplyFontSize(richService.Selection, fontSize);
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