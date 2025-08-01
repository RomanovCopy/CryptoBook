using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using Drawing = System.Drawing;
using Media = System.Windows.Media;

namespace CryptoBook.Interfaces
{
    public interface IFontFormatBar_ViewModel: IViewModel
    {


        public double FontSize { get; set; }
        public System.Drawing.FontStyle FontStyle { get; set; }
        public Media.FontFamily FontFamily { get; set; }    
        public Color FontColor { get; set; }
        public ITextDecorationItem TextDecoration { get; set; }
        public FontWeight FontWeight { get; set; }
        public FontStretch FontStretch { get; set; }
        /// <summary>
        /// Коллекция доступных размеров шрифта
        /// </summary>
        ObservableCollection<double> FontSizes { get; }
        /// <summary>
        /// Коллекция доступных стилей шрифта
        /// </summary>
        ObservableCollection<System.Drawing.FontStyle> FontStyles { get; }
        /// <summary>
        /// Коллекция доступных семейств шрифтов
        /// </summary>
        ObservableCollection<Media.FontFamily> FontFamilyes { get; }
        /// <summary>
        /// Коллекция доступных цветов шрифта
        /// </summary>
        ObservableCollection<System.Drawing.Color> FontColors { get; }
        /// <summary>
        /// Коллекция доступных видов форматирования текста
        /// </summary>
        ObservableCollection<ITextDecorationItem> TextDecorations { get; }
        /// <summary>
        /// Коллекция доступных FontWeight шрифта
        /// </summary>
        ObservableCollection<FontWeight> FontWeights { get;}
        /// <summary>
        /// Коллекция доступных FontStretch шрифта
        /// </summary>
        ObservableCollection<FontStretch> FontStretches { get; }



        ICommand SetFontStyleCommand { get; }
        ICommand SetFontWeightCommand { get; }
        ICommand SetFontStretchCommand { get; }
        ICommand SetFontFamilyCommand { get; }
        ICommand SetTextDecorationCommand { get; }
        ICommand SetFontColorCommand { get; }
        ICommand SetFontBackgroundCommand { get; }
        ICommand SetFontSizeCommand { get; }
        ICommand ClearFormattingCommand { get; }
    }
}