using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

using Drawing = System.Drawing;
using Media = System.Windows.Media;


namespace CryptoBook.Models
{
    internal class FontFormatBar_Model: ViewModelBase
    {
        private readonly IFontService fontService;
        private readonly IInlineService inlineService;
        private readonly IRichTextBoxService richService;

        internal ObservableCollection<double> FontSizes => fontService.FontSizes ?? throw new ArgumentNullException(nameof(fontService.FontSizes));
        internal ObservableCollection<System.Windows.FontStyle> FontStyles => fontService.FontStyles ?? throw new ArgumentNullException(nameof(fontService.FontStyles));
        internal ObservableCollection<Media.FontFamily> FontFamilyes => fontService.FontFamilyes ?? throw new ArgumentNullException(nameof(fontService.FontFamilyes));
        internal ObservableCollection<Drawing.Color> FontColors => fontService.FontColors ?? throw new ArgumentNullException(nameof(fontService.FontColors));
        internal ObservableCollection<TextDecorationItem> TextDecorations => fontService.TextDecorations ?? throw new ArgumentNullException(nameof(fontService.TextDecorations));
        internal ObservableCollection<System.Windows.FontWeight> FontWeights => fontService.FontWeights ?? throw new ArgumentNullException(nameof(fontService.FontWeights));
        internal ObservableCollection<System.Windows.FontStretch> FontStretches => fontService.FontStretches ?? throw new ArgumentNullException(nameof(fontService.FontStretches));

        public double FontSize { get=>fontSize; set=>SetProperty(ref fontSize, value); }
        double fontSize;
        public System.Windows.FontStyle FontStyle { get=>fontStyle; set=>SetProperty(ref fontStyle, value); }
        System.Windows.FontStyle fontStyle;
        public Media.FontFamily? FontFamily { get=>fontFamily; set=>SetProperty(ref fontFamily, value); }
        Media.FontFamily fontFamily;
        public Drawing.Color FontColor { get=>fontColor; set=>SetProperty(ref fontColor, value); }
        Drawing.Color fontColor;
        public Drawing.Color FontBackground { get => fontBackground; set => SetProperty(ref fontBackground, value); }
        Drawing.Color fontBackground;
        public TextDecorationItem? TextDecoration 
        { get=>textDecoration; 
            set=>SetProperty(ref textDecoration, value); }
        TextDecorationItem? textDecoration;
        public FontWeight FontWeight { get => fontWeight; set=>SetProperty(ref fontWeight, value); }
        FontWeight fontWeight;
        public FontStretch FontStretch { get=>fontStretch; set=>SetProperty(ref fontStretch, value); }
        FontStretch fontStretch;


        internal FontFormatBar_Model(IFontService service, IInlineService inlineService, IRichTextBoxService richService)
        {
            fontService = service ?? throw new ArgumentNullException(nameof(service));
            this.inlineService = inlineService ?? throw new ArgumentNullException(nameof(inlineService));
            this.richService = richService;
            InitializeValues();
        }

        void InitializeValues()
        {
            FontSize=fontService.DefaultFontSize;
            FontStyle=fontService.DefaultFontStyle;
            FontWeight=fontService.DefaultFontWeight;
            FontFamily=fontService.DefaultFontFamily;
            FontColor=fontService.DefaultFontColor;
            FontBackground =fontService.DefaultFontBackground;
            TextDecoration =fontService.DefaultTextDecoration;
        }



        internal bool CanExecute_SetFontStyleCommand(object? obj)
        {
            if(obj is not System.Windows.FontStyle fontStyle)
                return false;
            // Проверяем, что стиль шрифта доступен в коллекции
            return FontStyles.Contains(fontStyle);

        }
        internal void Execute_SetFontStyleCommand(object? obj)
        {
            if(obj is not System.Windows.FontStyle fontStyle)
                throw new ArgumentException("obj must be of type FontStyle", nameof(obj));
            //var inline = inlineService.GetInlineAtCaret();
            //var style=inlineService.GetEffectiveStyleAtCaret();
            var style=new InlineStyle();
            style.Set(TextElement.FontStyleProperty, fontStyle);

            if( style!=null)
            {
                //style.Set(TextElement.FontStyleProperty, fontStyle);
                inlineService.InsertRunAtCaret(new RunInsertOptions() { Style=style});
                //inlineService.ApplyStyle(inline, style);
            }

            //fontService.SetFontStyle(fontStyle);
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
            if(obj is not Media.FontFamily fontFamily)
                return false;
            return FontFamilyes.Contains(fontFamily);
        }
        internal void Execute_SetFontFamilyCommand(object? obj)
        {
            if(obj is not Media.FontFamily fontFamily)
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
                fontService.SetTextDecoration(fontService.DefaultTextDecoration.Decorations);
            } else
            {
                fontService.SetTextDecoration(decorations);
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



        internal bool CanExecute_Open(object? obj)
        {
            return true;
        }
        internal void Execute_Open(object? obj)
        {
            var style=inlineService.GetEffectiveStyleAtCaret();

            FontSize = style.Get<double>(TextElement.FontSizeProperty);
            FontFamily =style.Get<Media.FontFamily>(TextElement.FontFamilyProperty);
            FontWeight=style.Get<FontWeight>(TextElement.FontWeightProperty);
            FontStyle=style.Get<System.Windows.FontStyle>(TextElement.FontStyleProperty);
            FontStretch=style.Get<FontStretch>(TextElement.FontStretchProperty);
            var decor = style.Get<TextDecorationCollection>(Inline.TextDecorationsProperty);
            if(decor != null && decor.Count>0)
                TextDecoration = TextDecorations.FirstOrDefault(d => d.Decorations?.First() == decor.First());
            else
                TextDecoration = TextDecorations.FirstOrDefault(d => d.Name == "None");

            // Цвета: сначала точно, иначе ближайший
            if(TryGetDrawingColor(style, TextElement.ForegroundProperty, out var fore))
            {
                FontColor = FindColorInPalette(FontColors, fore, exactMatch: true)
                            ?? FindNearestColor(FontColors, fore);
            }
            if(TryGetDrawingColor(style, TextElement.BackgroundProperty, out var back))
            {
                FontBackground = FindColorInPalette(FontColors, back, exactMatch: true)
                                 ?? FindNearestColor(FontColors, back);
            }
        }

        internal bool CanExecute_Loaded(object? obj) { return true; }
        internal void Execute_Loaded(object? obj) { }

        internal bool CanExecute_Close(object? obj) { return true; }
        internal void Execute_Close(object? obj) { }

        internal bool CanExecute_Closing(object? obj) { return true; }
        internal void Execute_Closing(object? obj) { }

        internal bool CanExecute_Closed(object? obj) { return true; }
        internal void Execute_Closed(object? obj) { }



        // ===== ЧТЕНИЕ ЦВЕТА ИЗ InlineStyle =====

        private bool TryGetMediaColor(InlineStyle style, DependencyProperty dp, out Media.Color color)
        {
            color = default;
            if(!style.TryGetValue(dp, out var v) || v is null)
                return false;

            if(v is SolidColorBrush scb)
            {
                color = scb.Color;
                return true;
            }
            if(v is GradientBrush gb && gb.GradientStops.Count > 0)
            {
                // эвристика: берём первый стоп (или можно усреднять)
                color = gb.GradientStops[0].Color;
                return true;
            }
            if(v is Media.Brush mb)
            {
                // других кистей (VisualBrush/ImageBrush) в один цвет не сведёшь
                return false;
            }
            if(v is Media.Color mc)
            {
                color = mc;
                return true;
            }
            if(v is Drawing.Color dc)
            {
                color = Media.Color.FromArgb(dc.A, dc.R, dc.G, dc.B);
                return true;
            }
            return false;
        }

        private bool TryGetDrawingColor(InlineStyle style, DependencyProperty dp, out Drawing.Color color)
        {
            color = default;
            if(!TryGetMediaColor(style, dp, out var mc))
                return false;
            color = Drawing.Color.FromArgb(mc.A, mc.R, mc.G, mc.B);
            return true;
        }

        // ===== ЗАПИСЬ ЦВЕТА В InlineStyle =====

        private void SetMediaColor(InlineStyle style, DependencyProperty dp, Media.Color color)
        {
            // solid-кисть; можно добавить клон, если хотите всегда несвязанную кисть
            style[dp] = new SolidColorBrush(color);
        }

        private void SetDrawingColor(InlineStyle style, DependencyProperty dp, Drawing.Color color)
        {
            SetMediaColor(style, dp, Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        /// <summary>
        /// Поиск цвета в палитре (точное совпадение)
        /// </summary>
        /// <param name="palette">коллекция цветов</param>
        /// <param name="target">искомый цвет</param>
        /// <param name="exactMatch"></param>
        /// <returns></returns>
        private System.Drawing.Color? FindColorInPalette(
            System.Collections.ObjectModel.ObservableCollection<System.Drawing.Color> palette,
            System.Drawing.Color target,
            bool exactMatch)
        {
            if(exactMatch)
            {
                var found = palette.FirstOrDefault(c => c.ToArgb() == target.ToArgb());
                return found != default ? found : (System.Drawing.Color?)null;
            }
            return null;
        }

        // 4) Поиск ближайшего цвета (если точного нет)
        private System.Drawing.Color FindNearestColor(
            System.Collections.ObjectModel.ObservableCollection<System.Drawing.Color> palette,
            System.Drawing.Color target)
        {
            // евклидово расстояние в RGB
            return palette.OrderBy(c =>
            {
                int dr = c.R - target.R;
                int dg = c.G - target.G;
                int db = c.B - target.B;
                return dr * dr + dg * dg + db * db;
            }).FirstOrDefault();
        }

    }
}