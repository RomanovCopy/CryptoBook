using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using Drawing = System.Drawing;


namespace CryptoBook.Models
{
    internal class FontFormatBar_Model: ViewModelBase
    {
        private readonly IFontService fontService;

        internal ObservableCollection<double> FontSizes => fontService.FontSizes ?? throw new ArgumentNullException(nameof(fontService.FontSizes));
        internal ObservableCollection<Drawing.FontStyle> FontStyles => fontService.FontStyles ?? throw new ArgumentNullException(nameof(fontService.FontStyles));
        internal ObservableCollection<FontFamily> FontFamilies => fontService.FontFamilies ?? throw new ArgumentNullException(nameof(fontService.FontFamilies));
        internal ObservableCollection<Color> FontColors => fontService.FontColors ?? throw new ArgumentNullException(nameof(fontService.FontColors));
        internal ObservableCollection<ITextDecorationItem> TextDecorations => fontService.TextDecorations ?? throw new ArgumentNullException(nameof(fontService.TextDecorations));
        internal ObservableCollection<System.Windows.FontWeight> FontWeights => fontService.FontWeights ?? throw new ArgumentNullException(nameof(fontService.FontWeights));
        internal ObservableCollection<System.Windows.FontStretch> FontStretches => fontService.FontStretches ?? throw new ArgumentNullException(nameof(fontService.FontStretches));



        internal FontFormatBar_Model(IFontService service)
        {
            fontService = service ?? throw new ArgumentNullException(nameof(service));
        }



        internal bool CanExecute_SetFontStyleCommand(object? obj)
        {
            if(obj is not Drawing.FontStyle fontStyle)
                return false;
            // Проверяем, что стиль шрифта доступен в коллекции
            return FontStyles.Contains(fontStyle);

        }
        internal void Execute_SetFontStyleCommand(object? obj)
        {
            if(obj is not Drawing.FontStyle fontStyle)
                throw new ArgumentException("obj must be of type FontStyle", nameof(obj));
            fontService.SetFontStyle(fontStyle);
        }

        internal bool CanExecute_SetFontWeightCommand(object? obj)
        {
            if(obj is not System.Windows.FontWeight fontWeight)
                return false;
            return FontWeights.Contains(fontWeight);
        }
        internal void Execute_SetFontWeightCommand(object? obj)
        {
            if(obj is not System.Windows.FontWeight fontweight)
                throw new ArgumentException("obj must be of type FontWeight", nameof(obj));
            fontService.SetFontWeight(fontweight);
        }

        internal bool CanExecute_SetFontStretchCommand(object? obj)
        {
            if(obj is not System.Windows.FontStretch fontStretch)
                return false;
            return FontStretches.Contains(fontStretch);
        }
        internal void Execute_SetFontStretchCommand(object? obj)
        {
            if(obj is not System.Windows.FontStretch fontStretch)
                throw new ArgumentException("obj must be of type FontStretch", nameof(obj));
            fontService.SetFontStretch(fontStretch);
        }

        internal bool CanExecute_SetFontFamilyCommand(object? obj)
        {
            if(obj is not FontFamily fontFamily)
                return false;
            return FontFamilies.Contains(fontFamily);
        }
        internal void Execute_SetFontFamilyCommand(object? obj)
        {
            if(obj is not FontFamily fontFamily)
                throw new ArgumentException("obj must be of type FontFamily", nameof(obj));
            fontService.SetFontFamily(fontFamily);
        }


        internal bool CanExecute_SetTextDecorationCommand(object? obj)
        {
            if(obj is not ITextDecorationItem textDecoration)
                return false;
            return TextDecorations.Contains(textDecoration);
        }

        internal void Execute_SetTextDecorationCommand(object? obj)
        {
            if(obj is not ITextDecorationItem item)
                throw new ArgumentException("obj must be of type ITextDecorationItem", nameof(obj));

            var decorations = item.Decorations;

            if(decorations is null || decorations.Count == 0)
            {
                // "Нет" — снимаем декорации
                fontService.SetTextDecoration(null);
            } else
            {
                // Применяем первый элемент
                fontService.SetTextDecoration(decorations[0]);
            }
        }

        internal bool CanExecute_SetFontColorCommand(object? obj)
        {
            if(obj is not Drawing.Color fontColor)
                return false;
            return FontColors.Contains(fontColor);
        }
        internal void Execute_SetFontColorCommand(object? obj)
        {
            if(obj is not Drawing.Color fontColor)
                throw new ArgumentException("obj must be of type Color", nameof(obj));
            fontService.SetFontColor(fontColor);
        }


        internal bool CanExecute_SetFontBackgroundCommand(object? obj)
        {
            if(obj is not Drawing.Color fontBackgroundColor)
                return false;
            return FontColors.Contains(fontBackgroundColor);
        }
        internal void Execute_SetFontBackgroundCommand(object? obj)
        {
            if(obj is not Drawing.Color fontBackgroundColor)
                throw new ArgumentException("obj must be of type Color", nameof(obj));
            fontService.SetFontBackground(fontBackgroundColor);
        }

        internal bool CanExecute_SetFontSizeCommand(object? obj)
        {
            if(obj is not double fontSize)
                return false;
            return FontSizes.Contains(fontSize);
        }
        internal void Execute_SetFontSizeCommand(object? obj)
        {
            if(obj is not double fontSize)
                throw new ArgumentException("obj must be of type double", nameof(obj));
            fontService.SetFontSize(fontSize);
        }

        internal bool CanExecute_ClearFormattingCommand(object? obj)
        {
            if(obj is not null)
                return false;
            return true;
        }
        internal void Execute_ClearFormattingCommand(object? obj)
        {
            fontService.ClearFormatting();
        }





        internal bool CanExecute_Loaded(object? obj) { return true; }
        internal void Execute_Loaded(object? obj) { }

        internal bool CanExecute_Close(object? obj) { return true; }
        internal void Execute_Close(object? obj) { }

        internal bool CanExecute_Closing(object? obj) { return true; }
        internal void Execute_Closing(object? obj) { }

        internal bool CanExecute_Closed(object? obj) { return true; }
        internal void Execute_Closed(object? obj) { }



    }
}