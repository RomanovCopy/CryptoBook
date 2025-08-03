using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Media = System.Windows.Media;
using Drawing = System.Drawing;
using CryptoBook.Infrastructure;

namespace CryptoBook.Interfaces
{
    public interface IFontService
    {
        /// <summary>
        /// Interface для работы с RichTextBox
        /// </summary>
        IRichTextBoxService Service { get; set; }
        /// <summary>
        /// высота шрифта по умолчанию
        /// </summary>
        double DefaultFontSize { get; set; }
        /// <summary>
        /// FontStyle шрифта по умолчанию
        /// </summary>
        System.Windows.FontStyle DefaultFontStyle { get; set; }
        /// <summary>
        /// FontFamily шрифта по умолчанию
        /// </summary>
        Media.FontFamily DefaultFontFamily { get; set; }
        /// <summary>
        /// FontColor шрифта по умолчанию
        /// </summary>
        Drawing.Color DefaultFontColor { get; set; }
        /// <summary>
        /// FontBackground шрифта по умолчанию
        /// </summary>
        Drawing.Color DefaultFontBackground { get; set; }
        /// <summary>
        /// TextDecoration шрифта по умолчанию
        /// </summary>
        TextDecorationItem DefaultTextDecoration { get; set; }
        /// <summary>
        /// FontWeight шрифта по умолчанию
        /// </summary>
        FontWeight DefaultFontWeight { get; set; }
        /// <summary>
        /// FontStretch шрифта по умолчанию
        /// </summary>
        FontStretch DefaultFontStretch { get; set; }

        /// <summary>
        /// Коллекция доступных размеров шрифта
        /// </summary>
        ObservableCollection<double> FontSizes { get; set; }
        /// <summary>
        /// Коллекция доступных стилей шрифта
        /// </summary>
        ObservableCollection<System.Windows.FontStyle> FontStyles { get; set; }
        /// <summary>
        /// Коллекция доступных семейств шрифтов
        /// </summary>
        ObservableCollection<Media.FontFamily> FontFamilyes { get; set; }
        /// <summary>
        /// Коллекция доступных цветов шрифта
        /// </summary>
        ObservableCollection<System.Drawing.Color> FontColors { get; set; }
        /// <summary>
        /// Коллекция доступных видов форматирования текста
        /// </summary>
        ObservableCollection<TextDecorationItem> TextDecorations { get; set; }
        /// <summary>
        /// Коллекция доступных FontWeight шрифта
        /// </summary>
        ObservableCollection<FontWeight> FontWeights { get; set; }
        /// <summary>
        /// Коллекция доступных FontStretch шрифта
        /// </summary>
        ObservableCollection<FontStretch> FontStretches { get; set; }



        void SetFontStyle(System.Windows.FontStyle? fontStyle);
        void SetFontWeight(FontWeight? fontWeight);
        void SetFontStretch(FontStretch? fontStretch);
        void SetFontFamily(Media.FontFamily? fontFamily);
        void SetTextDecoration(TextDecorationCollection decoration);
        void SetFontColor(System.Drawing.Color? fontColor);
        void SetFontBackground(System.Drawing.Color? fontBackground);
        void SetFontSize(double fontSize);

        void ClearFormatting();
    }
}
