using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using Drawing = System.Drawing;
using Media= System.Windows.Media;

namespace CryptoBook.ViewModels
{
    public class FontFormatBar_ViewModel: ViewModelBase, IFontFormatBar_ViewModel
    {
        private readonly FontFormatBar_Model model;

        // IFontFormatBar_ViewModel implementation

        public double FontSize { get=>model.FontSize; set=>model.FontSize=value; }
        public System.Windows.FontStyle FontStyle { get => model.FontStyle; set => model.FontStyle=value; }
        public Media.FontFamily FontFamily { get => model.FontFamily; set => model.FontFamily=value; }
        public Color FontColor { get => model.FontColor; set => model.FontColor=value; }
        public Drawing.Color FontBackground { get => model.FontBackground; set => model.FontBackground=value; }
        public TextDecorationItem TextDecoration { get => model.TextDecoration; set => model.TextDecoration=value; }
        public FontWeight FontWeight { get => model.FontWeight; set => model.FontWeight=value; }
        public FontStretch FontStretch { get => model.FontStretch; set => model.FontStretch=value; }


        public ObservableCollection<double> FontSizes => model.FontSizes;
        public ObservableCollection<System.Windows.FontStyle> FontStyles => model.FontStyles;
        public ObservableCollection<Media.FontFamily> FontFamilyes => model.FontFamilyes;
        public ObservableCollection<Color> FontColors => model.FontColors;
        public ObservableCollection<TextDecorationItem> TextDecorations  => model.TextDecorations;
        public ObservableCollection<FontWeight> FontWeights => model.FontWeights;
        public ObservableCollection<FontStretch> FontStretches => model.FontStretches;


        // Constructor
        public FontFormatBar_ViewModel(IFontService service, IRichTextBoxService richService)
        {
            model = new FontFormatBar_Model(service ?? throw new ArgumentNullException(nameof(service)), 
                richService??throw new ArgumentNullException(nameof(richService)));
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }



        // IFontFormatBar_ViewModel implementation

        public ICommand SetFontStyleCommand => setFontStyleCommand??=new RelayCommand(model.Execute_SetFontStyleCommand, model.CanExecute_SetFontStyleCommand);
        RelayCommand setFontStyleCommand;

        public ICommand SetFontWeightCommand => 
            setFontWeightCommand ??= new RelayCommand(model.Execute_SetFontWeightCommand, model.CanExecute_SetFontWeightCommand);
        RelayCommand setFontWeightCommand;
        public ICommand SetFontStretchCommand => 
            setFontStretchCommand ??= new RelayCommand(model.Execute_SetFontStretchCommand, model.CanExecute_SetFontStretchCommand);
        RelayCommand setFontStretchCommand;

        public ICommand SetFontFamilyCommand =>
            setFontFamilyCommand ??= new RelayCommand(model.Execute_SetFontFamilyCommand, model.CanExecute_SetFontFamilyCommand);
        RelayCommand setFontFamilyCommand;

        public ICommand SetTextDecorationCommand => 
            setTextDecorationCommand ??= new RelayCommand(model.Execute_SetTextDecorationCommand, model.CanExecute_SetTextDecorationCommand);
        RelayCommand setTextDecorationCommand;

        public ICommand SetFontColorCommand => 
            setFontColorCommand ??= new RelayCommand(model.Execute_SetFontColorCommand, model.CanExecute_SetFontColorCommand);
        RelayCommand setFontColorCommand;

        public ICommand SetFontBackgroundCommand => 
            setFontBackgroundCommand ??= new RelayCommand(model.Execute_SetFontBackgroundCommand, model.CanExecute_SetFontBackgroundCommand);
        RelayCommand setFontBackgroundCommand;

        public ICommand SetFontSizeCommand => 
            setFontSizeCommand ??= new RelayCommand(model.Execute_SetFontSizeCommand, model.CanExecute_SetFontSizeCommand);
        RelayCommand setFontSizeCommand;

        public ICommand SetTextAlignmentCommand => setTextAlignmentCommand ??= new RelayCommand(model.Execute_SetTextAlignmentCommand, model.CanExecute_SetTextAlignmentCommand);
        RelayCommand setTextAlignmentCommand;

        public ICommand ClearFormattingCommand => 
            clearFormattingCommand ??= new RelayCommand(model.Execute_ClearFormattingCommand, model.CanExecute_ClearFormattingCommand);
        RelayCommand clearFormattingCommand;


        // IViewModel implementation

        public ICommand Loaded => 
            loadedCommand ??= new RelayCommand(model.Execute_Loaded, model.CanExecute_Loaded);
        RelayCommand loadedCommand;

        public ICommand Close => 
            closeCommand ??= new RelayCommand(model.Execute_Close, model.CanExecute_Close);
        RelayCommand closeCommand;

        public ICommand Closing => 
            closingCommand ??= new RelayCommand(model.Execute_Closing, model.CanExecute_Closing);
        RelayCommand closingCommand;

        public ICommand Closed => 
            closedCommand ??= new RelayCommand(model.Execute_Closed, model.CanExecute_Closed);
        RelayCommand closedCommand;

    }
}