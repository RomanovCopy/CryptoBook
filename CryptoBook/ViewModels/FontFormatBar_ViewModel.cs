using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class FontFormatBar_ViewModel: ViewModelBase, IFontFormatBar_ViewModel
    {
        private readonly FontFormatBar_Model model;

        public ObservableCollection<double> FontSizes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ObservableCollection<System.Drawing.FontStyle> FontStyles { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ObservableCollection<FontFamily> FontFamilies { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ObservableCollection<Color> FontColors { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ObservableCollection<ITextDecorationItem> TextDecorations { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ObservableCollection<FontWeight> FontWeights { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ObservableCollection<FontStretch> FontStretches { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }


        public FontFormatBar_ViewModel(ILifetimeScope scope)
        {
            this.scope = scope;
            model = new FontFormatBar_Model(scope ?? throw new ArgumentNullException(nameof(scope)));
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand SetFontStyleCommand => throw new NotImplementedException();

        public ICommand SetFontWeightCommand => throw new NotImplementedException();

        public ICommand SetFontStretchCommand => throw new NotImplementedException();

        public ICommand SetFontFamilyCommand => throw new NotImplementedException();

        public ICommand SetTextDecorationCommand => throw new NotImplementedException();

        public ICommand SetFontColorCommand => throw new NotImplementedException();

        public ICommand SetFontBackgroundCommand => throw new NotImplementedException();

        public ICommand SetFontSizeCommand => throw new NotImplementedException();

        public ICommand ClearFormattingCommand => throw new NotImplementedException();

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();
    }
}