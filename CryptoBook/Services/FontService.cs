using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Drarwing = System.Drawing;
using System.Linq;
using Windows.Media.Core;
using Media = System.Windows.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CryptoBook.Infrastructure;

namespace CryptoBook.Services
{
    public class FontService : IFontService
    {
        public IRichTextBoxService Service { get; set; }

        public ObservableCollection<double> FontSizes { get; set; }
        public ObservableCollection<Drarwing.FontStyle> FontStyles { get; set; }
        public ObservableCollection<Drarwing.FontFamily> FontFamilies { get; set; }
        public ObservableCollection<Drarwing.Color> FontColors { get; set; }
        public ObservableCollection<ITextDecorationItem> TextDecorations { get; set; }
        public ObservableCollection<FontWeight> FontWeights { get; set; }
        public ObservableCollection<FontStretch> FontStretches { get; set; }



        public FontService(IRichTextBoxService service)
        {
            Service = service;
            InitializeCollections();

        }

        private void InitializeCollections()
        {
            FontSizes = new ObservableCollection<double>(new double[]
            {
                8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72
            });

            FontStyles = new ObservableCollection<Drarwing.FontStyle>(
                Enum.GetValues(typeof(Drarwing.FontStyle)).Cast<Drarwing.FontStyle>());

            FontFamilies = new ObservableCollection<Drarwing.FontFamily>(
                Drarwing.FontFamily.Families);

            FontColors = new ObservableCollection<Drarwing.Color>(
                new Drarwing.Color[]
                {
                    Drarwing.Color.Black,
                    Drarwing.Color.White,
                    Drarwing.Color.Red,
                    Drarwing.Color.Green,
                    Drarwing.Color.Blue,
                    Drarwing.Color.Yellow,
                    Drarwing.Color.Gray,
                    Drarwing.Color.Orange,
                    Drarwing.Color.Purple,
                    Drarwing.Color.Brown
                });

            TextDecorations = new ObservableCollection<ITextDecorationItem>
            {
                new TextDecorationItem { Name = "Нет", Decorations = null },
                new TextDecorationItem { Name = "Подчеркнутый", Decorations = System.Windows.TextDecorations.Underline },
                new TextDecorationItem { Name = "Зачеркнутый", Decorations = System.Windows.TextDecorations.Strikethrough },
                new TextDecorationItem { Name = "Сверху", Decorations = System.Windows.TextDecorations.OverLine },
                new TextDecorationItem { Name = "Базовая линия", Decorations = System.Windows.TextDecorations.Baseline }
            };

            FontWeights = new ObservableCollection<System.Windows.FontWeight>
            {
                System.Windows.FontWeights.Thin,
                System.Windows.FontWeights.ExtraLight,
                System.Windows.FontWeights.Light,
                System.Windows.FontWeights.Normal,
                System.Windows.FontWeights.Medium,
                System.Windows.FontWeights.SemiBold,
                System.Windows.FontWeights.Bold,
                System.Windows.FontWeights.ExtraBold,
                System.Windows.FontWeights.Black
            };

            FontStretches = new ObservableCollection<System.Windows.FontStretch>
            {
                System.Windows.FontStretches.UltraCondensed,
                System.Windows.FontStretches.ExtraCondensed,
                System.Windows.FontStretches.Condensed,
                System.Windows.FontStretches.SemiCondensed,
                System.Windows.FontStretches.Normal,
                System.Windows.FontStretches.SemiExpanded,
                System.Windows.FontStretches.Expanded,
                System.Windows.FontStretches.ExtraExpanded,
                System.Windows.FontStretches.UltraExpanded
            };
        }

        public void SetFontStyle(Drarwing.FontStyle fontStyle)
        {
            throw new NotImplementedException();
        }

        public void SetFontWeight(FontWeight fontWeight)
        {
            throw new NotImplementedException();
        }

        public void SetFontStretch(FontStretch fontStretch)
        {
            throw new NotImplementedException();
        }

        public void SetFontFamily(Drarwing.FontFamily fontFamily)
        {
            throw new NotImplementedException();
        }

        public void SetTextDecoration(TextDecoration decoration)
        {
            throw new NotImplementedException();
        }

        public void SetFontColor(Drarwing.Color fontColor)
        {
            throw new NotImplementedException();
        }

        public void SetFontBackground(Drarwing.Color fontBackground)
        {
            throw new NotImplementedException();
        }

        public void SetFontSize(double fontSize)
        {
            throw new NotImplementedException();
        }

        public void ClearFormatting()
        {
            throw new NotImplementedException();
        }
    }
}
