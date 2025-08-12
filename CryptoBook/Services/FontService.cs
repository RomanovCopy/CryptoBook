using CryptoBook.Interfaces;
using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Media.Core;
using Media = System.Windows.Media;
using Drawing = System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;


using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation.Text;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CryptoBook.Services
{
    public class FontService: IFontService
    {
        public IRichTextBoxService Service { get; set; }

        public double DefaultFontSize { get => defaultFontSize; set => defaultFontSize = value; }
        double defaultFontSize;
        public System.Windows.FontStyle DefaultFontStyle { get => defaultFontStyle; set => defaultFontStyle = value; }
        System.Windows.FontStyle defaultFontStyle;
        public Media.FontFamily DefaultFontFamily { get => defaultFontFamily; set => defaultFontFamily = value; }
        Media.FontFamily defaultFontFamily;
        public Drawing.Color DefaultFontColor { get => defaultFontColor; set => defaultFontColor = value; }
        Drawing.Color defaultFontColor;
        public Drawing.Color DefaultFontBackground { get => defaultFontBackground; set => defaultFontBackground = value; }
        Drawing.Color defaultFontBackground;
        public TextDecorationItem DefaultTextDecoration { get => defaultTextDecoration; set => defaultTextDecoration = value; }
        TextDecorationItem defaultTextDecoration;
        public FontWeight DefaultFontWeight { get => defaultFontWeight; set => defaultFontWeight = value; }
        FontWeight defaultFontWeight;
        public FontStretch DefaultFontStretch { get => defaultFontStretch; set => defaultFontStretch = value; }
        FontStretch defaultFontStretch;


        public ObservableCollection<double> FontSizes { get; set; }
        public ObservableCollection<System.Windows.FontStyle> FontStyles { get; set; }
        public ObservableCollection<Media.FontFamily> FontFamilyes { get; set; }
        public ObservableCollection<Drawing.Color> FontColors { get; set; }
        public ObservableCollection<TextDecorationItem> TextDecorations { get; set; }
        public ObservableCollection<FontWeight> FontWeights { get; set; }
        public ObservableCollection<FontStretch> FontStretches { get; set; }



        public FontService(IRichTextBoxService service)
        {
            Service = service;
            InitializeCollections();
            InitializeDefaultValues();
            SetDefaultValues();
        }


        public void SetFontStyle(System.Windows.FontStyle? fontStyle)
        {
            if(fontStyle is System.Windows.FontStyle style)
            {
                if(Service.Selection.IsEmpty)
                {
                    TextPointer caret = Service.CaretPosition;
                    var oldrun = caret.Parent as Run;
                    // Попробуем найти Inline, к которому привязан курсор
                    Inline currentInline = caret.Parent as Inline;
                    // Создаем новый Run
                    Run newRun = new("");
                    newRun.FontStyle = style;
                    // Если нашли Inline — вставляем до или после него
                    if(currentInline != null && currentInline.Parent is Paragraph paragraph)
                    {
                        paragraph.Inlines.InsertAfter(currentInline, newRun);
                        // Переместим курсор за вставленным Run
                        Service.CaretPosition = newRun.ElementEnd;
                    } else if(caret.Paragraph != null)
                    {
                        // Курсор не в Inline, но есть Paragraph — просто добавим Run в конец
                        caret.Paragraph.Inlines.Add(newRun);
                        Service.CaretPosition = newRun.ElementEnd;
                    }
                    if(oldrun != null)
                    {
                        // Копируем форматирование из старого Run
                        CopyFormattingExcept(oldrun, newRun, TextElement.FontStyleProperty, style);
                    }
                    Service.Focus();
                } else
                {
                    ToggleOrClearFormatting(Service.Selection, TextElement.FontStyleProperty, style);
                }
            }

        }
        public void SetFontWeight(FontWeight? fontWeight)
        {
            if(fontWeight is System.Windows.FontWeight weight)
            {
                if(Service.Selection.IsEmpty)
                {
                    TextPointer caret = Service.CaretPosition;
                    var oldrun = caret.Parent as Run;
                    // Попробуем найти Inline, к которому привязан курсор
                    Inline currentInline = caret.Parent as Inline;
                    // Создаем новый Run
                    Run newRun = new("");
                    newRun.FontWeight = weight;
                    // Если нашли Inline — вставляем до или после него
                    if(currentInline != null && currentInline.Parent is Paragraph paragraph)
                    {
                        paragraph.Inlines.InsertAfter(currentInline, newRun);
                        // Переместим курсор за вставленным Run
                        Service.CaretPosition = newRun.ElementEnd;
                    } else if(caret.Paragraph != null)
                    {
                        // Курсор не в Inline, но есть Paragraph — просто добавим Run в конец
                        caret.Paragraph.Inlines.Add(newRun);
                        Service.CaretPosition = newRun.ElementEnd;
                    }
                    if(oldrun != null)
                    {
                        // Копируем форматирование из старого Run
                        CopyFormattingExcept(oldrun, newRun, TextElement.FontWeightProperty, weight);
                    }
                    Service.Focus();
                } else
                {
                    ToggleOrClearFormatting(Service.Selection, TextElement.FontWeightProperty, weight);
                }
            }
        }
        public void SetFontStretch(FontStretch? fontStretch)
        {
            if(fontStretch is System.Windows.FontStretch stretch)
            {
                if(Service.Selection.IsEmpty)
                {
                    TextPointer caret = Service.CaretPosition;
                    var oldrun = caret.Parent as Run;
                    // Попробуем найти Inline, к которому привязан курсор
                    Inline currentInline = caret.Parent as Inline;
                    // Создаем новый Run
                    Run newRun = new("");
                    newRun.FontStretch = stretch;
                    // Если нашли Inline — вставляем до или после него
                    if(currentInline != null && currentInline.Parent is Paragraph paragraph)
                    {
                        paragraph.Inlines.InsertAfter(currentInline, newRun);
                        // Переместим курсор за вставленным Run
                        Service.CaretPosition = newRun.ElementEnd;
                    } else if(caret.Paragraph != null)
                    {
                        // Курсор не в Inline, но есть Paragraph — просто добавим Run в конец
                        caret.Paragraph.Inlines.Add(newRun);
                        Service.CaretPosition = newRun.ElementEnd;
                    }
                    if(oldrun != null)
                    {
                        // Копируем форматирование из старого Run
                        CopyFormattingExcept(oldrun, newRun, TextElement.FontStretchProperty, stretch);
                    }
                    Service.Focus();
                } else
                {
                    ToggleOrClearFormatting(Service.Selection, TextElement.FontStretchProperty, stretch);
                }
            }
        }
        public void SetFontFamily(Media.FontFamily? fontFamily)
        {
            if(fontFamily is Media.FontFamily family)
            {
                if(Service.Selection.IsEmpty)
                {
                    TextPointer caret = Service.CaretPosition;
                    var oldrun = caret.Parent as Run;
                    // Попробуем найти Inline, к которому привязан курсор
                    Inline currentInline = caret.Parent as Inline;
                    // Создаем новый Run
                    Run newRun = new("");
                    newRun.FontFamily = family;
                    // Если нашли Inline — вставляем до или после него
                    if(currentInline != null && currentInline.Parent is Paragraph paragraph)
                    {
                        paragraph.Inlines.InsertAfter(currentInline, newRun);
                        // Переместим курсор за вставленным Run
                        Service.CaretPosition = newRun.ElementEnd;
                    } else if(caret.Paragraph != null)
                    {
                        // Курсор не в Inline, но есть Paragraph — просто добавим Run в конец
                        caret.Paragraph.Inlines.Add(newRun);
                        Service.CaretPosition = newRun.ElementEnd;
                    }
                    if(oldrun != null)
                    {
                        // Копируем форматирование из старого Run
                        CopyFormattingExcept(oldrun, newRun, TextElement.FontFamilyProperty, family);
                    }
                    Service.Focus();
                } else
                {
                    ToggleOrClearFormatting(Service.Selection, TextElement.FontFamilyProperty, family);
                }
            }

        }
        public void SetTextDecoration(TextDecorationCollection fontDecoration)
        {
            if(Service.Selection.IsEmpty)
            {
                TextPointer caret = Service.CaretPosition;
                // Попробуем найти Inline, к которому привязан курсор
                Inline currentInline = caret.Parent as Inline;
                //находим родительский Run, если есть
                var oldrun = caret.Parent as Run;
                // Создаем новый Run
                Run newRun = new Run("");
                newRun.TextDecorations = fontDecoration;
                // Если нашли Inline — вставляем до или после него
                if(currentInline != null && currentInline.Parent is Paragraph paragraph)
                {
                    paragraph.Inlines.InsertAfter(currentInline, newRun);
                    // Переместим курсор за вставленным Run
                    Service.CaretPosition = newRun.ElementEnd;
                } else if(caret.Paragraph != null)
                {
                    // Курсор не в Inline, но есть Paragraph — просто добавим Run в конец
                    caret.Paragraph.Inlines.Add(newRun);
                    Service.CaretPosition = newRun.ElementEnd;
                }
                if(oldrun != null)
                {
                    // Копируем форматирование из родительского Run
                    CopyFormattingExcept(oldrun, newRun, Inline.TextDecorationsProperty, fontDecoration);
                }
                Service.Focus();
            } else
            {
                // Если выделение не пустое, применяем форматирование к выделенному тексту
                ToggleOrClearFormatting(Service.Selection, Inline.TextDecorationsProperty, fontDecoration);
            }
        }
        public void SetFontColor(Drawing.Color? fontColor)
        {
            if(fontColor is Drawing.Color color)
            {
                var brush = new Media.SolidColorBrush(Media.Color.FromArgb(color.A, color.R, color.G, color.B));
                if(Service.Selection.IsEmpty)
                {
                    TextPointer caret = Service.CaretPosition;
                    var oldrun = caret.Parent as Run;

                    // Попробуем найти Inline, к которому привязан курсор
                    Inline currentInline = caret.Parent as Inline;

                    // Создаем новый Run
                    Run newRun = new Run("");
                    newRun.Foreground = brush;
                    Service.CaretBrush = brush;

                    // Если нашли Inline — вставляем до или после него
                    if(currentInline != null && currentInline.Parent is Paragraph paragraph)
                    {
                        paragraph.Inlines.InsertAfter(currentInline, newRun);

                        // Переместим курсор за вставленным Run
                        Service.CaretPosition = newRun.ElementEnd;
                    } else if(caret.Paragraph != null)
                    {
                        // Курсор не в Inline, но есть Paragraph — просто добавим Run в конец
                        caret.Paragraph.Inlines.Add(newRun);
                        Service.CaretPosition = newRun.ElementEnd;
                    }
                    if(oldrun != null)
                    {
                        // Копируем форматирование из старого Run
                        CopyFormattingExcept(oldrun, newRun, TextElement.ForegroundProperty, brush);
                    }

                    Service.Focus();
                } else
                {
                    ToggleOrClearFormatting(Service.Selection, TextElement.ForegroundProperty, brush);
                }
            }
        }
        public void SetFontBackground(Drawing.Color? fontBackground)
        {
            if(fontBackground is Drawing.Color color)
            {
                var brush = new Media.SolidColorBrush(Media.Color.FromArgb(color.A, color.R, color.G, color.B));
                if(Service.Selection.IsEmpty)
                {
                    TextPointer caret = Service.CaretPosition;

                    // Попробуем найти Inline, к которому привязан курсор
                    Inline currentInline = caret.Parent as Inline;
                    var oldrun = caret.Parent as Run;

                    // Создаем новый Run
                    Run newRun = new Run("");
                    newRun.Background = brush;

                    // Если нашли Inline — вставляем до или после него
                    if(currentInline != null && currentInline.Parent is Paragraph paragraph)
                    {
                        paragraph.Inlines.InsertAfter(currentInline, newRun);

                        // Переместим курсор за вставленным Run
                        Service.CaretPosition = newRun.ElementEnd;
                    } else if(caret.Paragraph != null)
                    {
                        // Курсор не в Inline, но есть Paragraph — просто добавим Run в конец
                        caret.Paragraph.Inlines.Add(newRun);
                        Service.CaretPosition = newRun.ElementEnd;
                    }
                    if(oldrun != null)
                    {
                        // Копируем форматирование из старого Run
                        CopyFormattingExcept(oldrun, newRun, TextElement.BackgroundProperty, brush);
                    }
                    Service.Focus();
                } else
                {
                    ToggleOrClearFormatting(Service.Selection, TextElement.BackgroundProperty, brush);

                }


            }
        }
        public void SetFontSize(double fontSize)
        {
            if(fontSize is double size)
            {
                if(Service.Selection.IsEmpty)
                {
                    TextPointer caret = Service.CaretPosition;
                    var oldrun = caret.Parent as Run;

                    // Попробуем найти Inline, к которому привязан курсор
                    Inline currentInline = caret.Parent as Inline;

                    // Создаем новый Run
                    Run newRun = new Run("");
                    newRun.FontSize = size;

                    // Если нашли Inline — вставляем до или после него
                    if(currentInline != null && currentInline.Parent is Paragraph paragraph)
                    {
                        paragraph.Inlines.InsertAfter(currentInline, newRun);

                        // Переместим курсор за вставленным Run
                        Service.CaretPosition = newRun.ElementEnd;
                    } else if(caret.Paragraph != null)
                    {
                        // Курсор не в Inline, но есть Paragraph — просто добавим Run в конец
                        caret.Paragraph.Inlines.Add(newRun);
                        Service.CaretPosition = newRun.ElementEnd;
                    }
                    if(oldrun != null)
                    {
                        // Копируем форматирование из старого Run
                        CopyFormattingExcept(oldrun, newRun, TextElement.FontSizeProperty, size);
                    }

                    Service.Focus();

                } else
                {
                    ApplyFontSize(Service.Selection, size);

                }
            }
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



        private void SetDefaultValues()
        {
            SetFontSize(DefaultFontSize);
            Service.Document.FontSize = DefaultFontSize;
            SetFontStyle(DefaultFontStyle);
            Service.Document.FontStyle = DefaultFontStyle;
            SetFontWeight(DefaultFontWeight);
            Service.Document.FontWeight = DefaultFontWeight;
            SetFontStretch(DefaultFontStretch);
            Service.Document.FontStretch = DefaultFontStretch;
            SetFontColor(DefaultFontColor);
            Service.Document.Foreground = new Media.SolidColorBrush(Media.Color.FromArgb(DefaultFontColor.A, DefaultFontColor.R, DefaultFontColor.G, DefaultFontColor.B));
            SetTextDecoration(DefaultTextDecoration.Decorations);
            SetFontBackground(DefaultFontBackground);
            SetFontFamily(DefaultFontFamily);
        }
        private void InitializeDefaultValues()
        {
            DefaultFontSize = 16.0;
            DefaultFontStyle = System.Windows.FontStyles.Normal;
            DefaultFontFamily = FontFamilyes.FirstOrDefault(f => f != null && f.Source == "Consolas") ?? FontFamilyes[0];
            DefaultFontColor = FontColors.FirstOrDefault(c => c.Name == "Black");
            DefaultFontBackground = FontColors.FirstOrDefault(c => c.Name == "Transparent");
            DefaultTextDecoration = TextDecorations.FirstOrDefault(d => d.Name == "None") ?? new TextDecorationItem { Name = "None", Decorations = null };
            DefaultFontWeight = FontWeights.FirstOrDefault(f => f == System.Windows.FontWeights.Normal);
            DefaultFontStretch = FontStretches.FirstOrDefault(s => s == System.Windows.FontStretches.Normal);
        }
        private void InitializeCollections()
        {
            FontSizes = new ObservableCollection<double>(new double[]
            {
                8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72
            });

            var propertyes = typeof(FontStyles).GetProperties()
                .Where(p => p.PropertyType == typeof(System.Windows.FontStyle));
            FontStyles = new ObservableCollection<System.Windows.FontStyle>();
            foreach(var property in propertyes)
            {
                if(property != null && property.GetValue(null) is System.Windows.FontStyle style)
                    FontStyles.Add(style);
            }


            FontFamilyes =
            [
                new Media.FontFamily("Segoe UI"),          // Современный системный шрифт Windows
                new Media.FontFamily("Arial"),             // Классический без засечек
                new Media.FontFamily("Times New Roman"),   // С засечками, часто для печатного текста
                new Media.FontFamily("Calibri"),           // Стандартный в Microsoft Office
                new Media.FontFamily("Verdana"),           // Отличная читаемость на экране
                new Media.FontFamily("Tahoma"),            // Компактный и читаемый
                new Media.FontFamily("Consolas"),          // Моноширинный, идеален для кода
                new Media.FontFamily("Courier New"),       // Классический моноширинный
                new Media.FontFamily("Comic Sans MS"),     // Декоративный, "ручной"
                new Media.FontFamily("Georgia"),           // С засечками, более современный, чем Times
                new Media.FontFamily("Segoe UI Variable"), // Современный шрифт с переменной шириной
                new Media.FontFamily("Roboto"),            // Современный шрифт от Google
                new Media.FontFamily("Bahnschrift")         // Современный шрифт с геометрическим дизайном
            ];



            FontColors = new ObservableCollection<Drawing.Color>(
                new Drawing.Color[]
                {
                    Drawing.Color.Black,
                    Drawing.Color.White,
                    Drawing.Color.Red,
                    Drawing.Color.Green,
                    Drawing.Color.Blue,
                    Drawing.Color.Yellow,
                    Drawing.Color.Gray,
                    Drawing.Color.Orange,
                    Drawing.Color.Purple,
                    Drawing.Color.Brown,
                    Drawing.Color.Cyan,
                    Drawing.Color.Transparent
                });

            TextDecorations = new ObservableCollection<TextDecorationItem>
            {
                new TextDecorationItem { Name = "None", Decorations = null },
                new TextDecorationItem { Name = "Underline", Decorations = System.Windows.TextDecorations.Underline },
                new TextDecorationItem { Name = "Strikethrough", Decorations = System.Windows.TextDecorations.Strikethrough },
                new TextDecorationItem { Name = "OverLine", Decorations = System.Windows.TextDecorations.OverLine },
                new TextDecorationItem { Name = "Baseline", Decorations = System.Windows.TextDecorations.Baseline }
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
        private void ToggleOrClearFormatting(TextRange range, DependencyProperty property, object targetValue)
        {
            object current = range.GetPropertyValue(property);

            if(current == null)
                return;

            bool shouldRemove = current != DependencyProperty.UnsetValue && current.Equals(targetValue);

            if(property == Inline.TextDecorationsProperty)
            {
                range.ApplyPropertyValue(property, shouldRemove ? DefaultTextDecoration : targetValue);

            } else if(property == TextElement.FontWeightProperty)
            {
                range.ApplyPropertyValue(property, shouldRemove ? DefaultFontWeight : targetValue);
            } else if(property == TextElement.FontStyleProperty)
            {
                range.ApplyPropertyValue(property, shouldRemove ? DefaultFontStyle : targetValue);
            } else if(property == TextElement.FontStretchProperty)
            {
                range.ApplyPropertyValue(property, shouldRemove ? DefaultFontStretch : targetValue);
            } else if(property == TextElement.FontFamilyProperty)
            {
                range.ApplyPropertyValue(property, shouldRemove ? DefaultFontFamily : targetValue);
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
        private void CopyFormattingExcept(Run source, Run target, DependencyProperty exceptProperty, object newValue)
        {
            target.FontSize = exceptProperty == TextElement.FontSizeProperty ? (double)newValue : source.FontSize;
            target.FontFamily = exceptProperty == TextElement.FontFamilyProperty ? (Media.FontFamily)newValue : source.FontFamily;
            target.FontWeight = exceptProperty == TextElement.FontWeightProperty ? (FontWeight)newValue : source.FontWeight;
            target.FontStyle = exceptProperty == TextElement.FontStyleProperty ? (System.Windows.FontStyle)newValue : source.FontStyle;
            target.Foreground = exceptProperty == TextElement.ForegroundProperty ? (Media.Brush)newValue : source.Foreground;
            target.Background = exceptProperty == TextElement.BackgroundProperty ? (Media.Brush)newValue : source.Background;
            target.TextDecorations = exceptProperty == Inline.TextDecorationsProperty ? (TextDecorationCollection)newValue : source.TextDecorations;
        }

    }
}
