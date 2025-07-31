using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

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
        System.Drawing.FontStyle DefaultFontStyle { get; set; }
        /// <summary>
        /// FontFamily шрифта по умолчанию
        /// </summary>
        System.Drawing.FontFamily DefaultFontFamily { get; set; }
        /// <summary>
        /// FontColor шрифта по умолчанию
        /// </summary>
        System.Drawing.Color DefaultFontColor { get; set; }
        /// <summary>
        /// FontBackground шрифта по умолчанию
        /// </summary>
        System.Drawing.Color DefaultFontBackground { get; set; }
        /// <summary>
        /// TextDecoration шрифта по умолчанию
        /// </summary>
        TextDecoration DefaultTextDecoration { get; set; }
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
        ObservableCollection<System.Drawing.FontStyle> FontStyles { get; set; }
        /// <summary>
        /// Коллекция доступных семейств шрифтов
        /// </summary>
        ObservableCollection<System.Drawing.FontFamily> FontFamilies { get; set; }
        /// <summary>
        /// Коллекция доступных цветов шрифта
        /// </summary>
        ObservableCollection<System.Drawing.Color> FontColors { get; set; }
        /// <summary>
        /// Коллекция доступных видов форматирования текста
        /// </summary>
        ObservableCollection<ITextDecorationItem> TextDecorations { get; set; }
        /// <summary>
        /// Коллекция доступных FontWeight шрифта
        /// </summary>
        ObservableCollection<FontWeight> FontWeights { get; set; }
        /// <summary>
        /// Коллекция доступных FontStretch шрифта
        /// </summary>
        ObservableCollection<FontStretch> FontStretches { get; set; }



        void SetFontStyle(System.Drawing.FontStyle fontStyle);
        void SetFontWeight(FontWeight fontWeight);
        void SetFontStretch(FontStretch fontStretch);
        void SetFontFamily(System.Drawing.FontFamily fontFamily);
        void SetTextDecoration(TextDecoration decoration);
        void SetFontColor(System.Drawing.Color fontColor);
        void SetFontBackground(System.Drawing.Color fontBackground);
        void SetFontSize(double fontSize);

        void ClearFormatting();
    }
}
