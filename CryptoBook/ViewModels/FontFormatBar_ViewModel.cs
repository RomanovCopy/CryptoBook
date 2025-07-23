using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class FontFormatBar_ViewModel: ViewModelBase, IFontFormatBar_ViewModel
    {
        private readonly FontFormatBar_Model model;

        public FontFormatBar_ViewModel(ILifetimeScope scope)
        {
            model = new FontFormatBar_Model(scope ?? throw new ArgumentNullException(nameof(scope)));
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand BoldCommand => boldCommand ??= new RelayCommand(model.Execute_Bold, model.CanExecute_Bold);
        RelayCommand boldCommand;
        public ICommand ItalicCommand => italicCommand ??= new RelayCommand(model.Execute_Italic, model.CanExecute_Italic);
        RelayCommand italicCommand;
        public ICommand UnderlineCommand => underlineCommand ??= new RelayCommand(model.Execute_Underline, model.CanExecute_Underline);
        RelayCommand underlineCommand;
        public ICommand ClearFormattingCommand => clearFormattingCommand ??= new RelayCommand(model.Execute_ClearFormatting, model.CanExecute_ClearFormatting);
        RelayCommand clearFormattingCommand;
        public ICommand FontSizeCommand => fontSizeCommand ??= new RelayCommand(model.Execute_ChangeFontSize, model.CanExecute_ChangeFontSize);
        RelayCommand fontSizeCommand;
        public ICommand FontFamilyCommand => fontFamilyCommand ??= new RelayCommand(model.Execute_ChangeFontFamily, model.CanExecute_ChangeFontFamily);
        RelayCommand fontFamilyCommand;
        public ICommand TextAlligmentCommand => textAlligmentCommand ??= new RelayCommand(model.Execute_ChangeTextAlignment, model.CanExecute_ChangeTextAlignment);
        RelayCommand textAlligmentCommand;
        public ICommand ForegroundCommand => foregroundCommand ??= new RelayCommand(model.Execute_ChangeForeground, model.CanExecute_ChangeForeground);
        RelayCommand foregroundCommand;
        public ICommand BackgroundCommand => backgroundCommand ??= new RelayCommand(model.Execute_ChangeBackground, model.CanExecute_ChangeBackground);
        RelayCommand backgroundCommand;

        public ICommand Loaded => loadedCommand ??= new RelayCommand(model.OnLoaded, model.CanExecute_Loaded);
        RelayCommand loadedCommand;
        public ICommand Close => closeCommand ??= new RelayCommand(model.OnClose, model.CanExecute_Close);
        RelayCommand closeCommand;
        public ICommand Closing => closingCommand ??= new RelayCommand(model.OnClosing, model.CanExecute_Closing);
        RelayCommand closingCommand;
        public ICommand Closed => closedCommand ??= new RelayCommand(model.OnClosed, model.CanExecute_Closed);
        RelayCommand closedCommand;
    }
}