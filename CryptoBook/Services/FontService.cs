using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Drarwing = System.Drawing;
using System.Linq;
using Windows.Media.Core;
using Media = System.Windows.Media;
using Drawing= System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CryptoBook.Infrastructure;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public class FontService : IFontService
    {
        public IRichTextBoxService Service { get; set; }

        public double DefaultFontSize { get => defaultFontSize; set => defaultFontSize=value; }
        double defaultFontSize;
        public Drarwing.FontStyle DefaultFontStyle { get => defaultFontStyle; set => defaultFontStyle=value; }
        Drawing.FontStyle defaultFontStyle;
        public Drarwing.FontFamily DefaultFontFamily { get => defaultFontFamily; set => defaultFontFamily=value; }
        Drawing.FontFamily defaultFontFamily;
        public Drarwing.Color DefaultFontColor { get => defaultFontColor; set => defaultFontColor=value; }
        Drawing.Color defaultFontColor;
        public Drarwing.Color DefaultFontBackground { get => defaultFontBackground; set => defaultFontBackground=value; }
        Drawing.Color defaultFontBackground;
        public TextDecoration DefaultTextDecoration { get => defaultTextDecoration; set => defaultTextDecoration = value; }
        TextDecoration defaultTextDecoration;
        public FontWeight DefaultFontWeight { get => defaultFontWeight; set => defaultFontWeight=value; }
        FontWeight defaultFontWeight;  
        public FontStretch DefaultFontStretch { get => defaultFontStretch; set => defaultFontStretch=value; }
        FontStretch defaultFontStretch;

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
            ToggleOrClearFormatting(Service.Selection, TextElement.FontStyleProperty, fontStyle);
        }

        public void SetFontWeight(FontWeight fontWeight)
        {
            ToggleOrClearFormatting(Service.Selection, TextElement.FontWeightProperty, fontWeight);
        }

        public void SetFontStretch(FontStretch fontStretch)
        {
            ToggleOrClearFormatting(Service.Selection, TextElement.FontStretchProperty, fontStretch);
        }

        public void SetFontFamily(Drarwing.FontFamily fontFamily)
        {
            ToggleOrClearFormatting(Service.Selection, TextElement.FontFamilyProperty, fontFamily);
        }

        public void SetTextDecoration(TextDecoration decoration)
        {
            ToggleOrClearFormatting(Service.Selection, Inline.TextDecorationsProperty, decoration);
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
            ApplyFontSize(Service.Selection, fontSize);
        }

        public void ClearFormatting()
        {
            Service.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, DefaultFontStyle);
            Service.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, DefaultFontWeight);
            Service.Selection.ApplyPropertyValue(TextElement.FontStretchProperty, DefaultFontStretch);
            Service.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, DefaultFontFamily);
            Service.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, DefaultTextDecoration);
            Service.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, DefaultFontSize);
        }


        private void ToggleOrClearFormatting(TextRange range, DependencyProperty property, object targetValue)
        {
            object current = range.GetPropertyValue(property);

            bool shouldRemove = current != DependencyProperty.UnsetValue && current.Equals(targetValue);

            if(property == Inline.TextDecorationsProperty)
            {
                // Особый случай для подчеркивания
                range.ApplyPropertyValue(property, shouldRemove ? DefaultTextDecoration : targetValue);

            } else if(property == TextElement.FontWeightProperty)
            {
                range.ApplyPropertyValue(property, shouldRemove ? DefaultFontWeight : targetValue);
            } else if(property == TextElement.FontStyleProperty)
            {
                range.ApplyPropertyValue(property, shouldRemove ? DefaultFontStyle : targetValue);
            } else
            {
                // Общий случай
                range.ApplyPropertyValue(property, shouldRemove ? null : targetValue);
            }
        }

        private void ApplyFontSize(TextSelection selection, double fontSize)
        {
            if(selection == null)
                return;

            if(selection.IsEmpty)
            {
                TextPointer caret = Service.CaretPosition;
                TextPointer start = null;
                TextPointer end = null;
                FlowDocument document = Service.Document;

                if(document == null)
                    return;
                // Предпочитаем символ перед курсором, если он есть, иначе — за ним

                if(caret.CompareTo(document.ContentStart) == 0)
                {
                    start = caret;
                    end = caret.GetPositionAtOffset(1, LogicalDirection.Forward);

                } else
                {
                    start = caret.GetPositionAtOffset(-1, LogicalDirection.Backward);
                    end = caret.GetPositionAtOffset(0, LogicalDirection.Forward);
                }
                // Применим, только если есть что форматировать
                if(start != null && end != null && !start.Equals(end))
                {
                    var range = new TextRange(start, end);
                    range.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
                    Service.CaretPosition = start;
                }
                Service.Focus();
            } else
            {
                var range = new TextRange(selection.Start, selection.End);
                range.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
            }
        }

    }
}
