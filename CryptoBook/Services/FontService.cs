using CryptoBook.Interfaces;
using CryptoBook.Infrastructure;

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

using Drawing = System.Drawing;
using Media = System.Windows.Media;

namespace CryptoBook.Services
{
    public class FontService: IFontService
    {
        private readonly IDocumentBackgroundPreferenceStore documentBackgroundPreferenceStore;
        private readonly IDocumentAppearanceDefaults appearanceDefaults;
        private readonly IDocumentSession? documentSession;
        private Drawing.Color? storedDocumentBackground;

        public event EventHandler? DocumentBackgroundChanged;

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
        public Drawing.Color DocumentBackground { get; private set; }
        public bool HasDocumentBackgroundImage =>
            Service.Document.Background is Media.ImageBrush;
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



        public FontService(
            IRichTextBoxService service,
            IInlineService inlineService,
            IDocumentBackgroundPreferenceStore documentBackgroundPreferenceStore,
            IDocumentAppearanceDefaults appearanceDefaults,
            IDocumentSession? documentSession = null)
        {
            Service = service ?? throw new ArgumentNullException(nameof(service));
            _ = inlineService ?? throw new ArgumentNullException(nameof(inlineService));
            this.documentBackgroundPreferenceStore =
                documentBackgroundPreferenceStore ??
                throw new ArgumentNullException(
                    nameof(documentBackgroundPreferenceStore));
            this.appearanceDefaults = appearanceDefaults ??
                throw new ArgumentNullException(nameof(appearanceDefaults));
            this.documentSession = documentSession;
            InitializeCollections();
            InitializeDefaultValues();
            SetDefaultValues();
        }
        public void SetFontStyle(System.Windows.FontStyle? fontStyle)
        {
            if(fontStyle is System.Windows.FontStyle style)
                ApplyCharacterProperty(System.Windows.Documents.TextElement.FontStyleProperty, style);

        }
        public void SetFontWeight(FontWeight? fontWeight)
        {
            if(fontWeight is System.Windows.FontWeight weight)
                ApplyCharacterProperty(System.Windows.Documents.TextElement.FontWeightProperty, weight);
        }
        public void SetFontStretch(FontStretch? fontStretch)
        {
            if(fontStretch is System.Windows.FontStretch stretch)
                ApplyCharacterProperty(System.Windows.Documents.TextElement.FontStretchProperty, stretch);
        }
        public void SetFontFamily(Media.FontFamily? fontFamily)
        {
            if(fontFamily is Media.FontFamily family)
                ApplyCharacterProperty(System.Windows.Documents.TextElement.FontFamilyProperty, family);

        }
        public void SetTextDecoration(TextDecorationCollection fontDecoration)
        {
            ApplyCharacterProperty(Inline.TextDecorationsProperty, fontDecoration);
        }
        public void SetFontColor(Drawing.Color? fontColor)
        {
            if(fontColor is Drawing.Color color)
            {
                var brush = new Media.SolidColorBrush(Media.Color.FromArgb(color.A, color.R, color.G, color.B));
                ApplyCharacterProperty(System.Windows.Documents.TextElement.ForegroundProperty, brush);
                Service.CaretBrush = brush;
            }
        }
        public void SetFontBackground(Drawing.Color? fontBackground)
        {
            if(fontBackground is Drawing.Color color)
            {
                var brush = new Media.SolidColorBrush(Media.Color.FromArgb(color.A, color.R, color.G, color.B));
                ApplyCharacterProperty(System.Windows.Documents.TextElement.BackgroundProperty, brush);
            }
        }
        public void SetDocumentBackground(Drawing.Color? documentBackground)
        {
            if(documentBackground is not Drawing.Color color)
                return;

            if(!ApplyDocumentBackground(color))
                return;

            documentBackgroundPreferenceStore.Save(color);
            documentSession?.MarkDirty();
            NotifyDocumentBackgroundChanged();
        }
        public void SetDocumentBackgroundImage(BitmapSource backgroundImage)
        {
            ArgumentNullException.ThrowIfNull(backgroundImage);

            if(GetDrawingColor(Service.Document.Background) is Drawing.Color color)
                DocumentBackground = color;

            var brush = new Media.ImageBrush(backgroundImage)
            {
                Stretch = Media.Stretch.UniformToFill,
                AlignmentX = Media.AlignmentX.Center,
                AlignmentY = Media.AlignmentY.Center
            };
            Service.Document.Background = brush;
            Service.BackGround = brush;
            documentSession?.MarkDirty();
            NotifyDocumentBackgroundChanged();
        }
        public void ClearDocumentBackgroundImage()
        {
            if(!HasDocumentBackgroundImage)
                return;

            ApplyDocumentBackground(DocumentBackground);
            documentSession?.MarkDirty();
            NotifyDocumentBackgroundChanged();
        }
        public void SetFontSize(double fontSize)
        {
            if(double.IsNaN(fontSize) || double.IsInfinity(fontSize) || fontSize <= 0)
                return;

            ApplyCharacterProperty(System.Windows.Documents.TextElement.FontSizeProperty, fontSize);
        }
        public void ClearFormatting()
        {
            ApplyCharacterProperty(System.Windows.Documents.TextElement.FontStyleProperty, DefaultFontStyle);
            ApplyCharacterProperty(System.Windows.Documents.TextElement.FontWeightProperty, DefaultFontWeight);
            ApplyCharacterProperty(System.Windows.Documents.TextElement.FontStretchProperty, DefaultFontStretch);
            ApplyCharacterProperty(System.Windows.Documents.TextElement.FontFamilyProperty, DefaultFontFamily);
            ApplyCharacterProperty(Inline.TextDecorationsProperty, DefaultTextDecoration.Decorations);
            ApplyCharacterProperty(System.Windows.Documents.TextElement.FontSizeProperty, DefaultFontSize);

            var foreground = CreateBrush(DefaultFontColor);
            var background = CreateBrush(DefaultFontBackground);
            ApplyCharacterProperty(System.Windows.Documents.TextElement.ForegroundProperty, foreground);
            ApplyCharacterProperty(System.Windows.Documents.TextElement.BackgroundProperty, background);
            Service.CaretBrush = foreground;
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
            Service.Document.Foreground = CreateBrush(DefaultFontColor);
            SetTextDecoration(DefaultTextDecoration.Decorations);
            SetFontBackground(DefaultFontBackground);
            SetFontFamily(DefaultFontFamily);
            if(storedDocumentBackground is Drawing.Color documentBackground)
                ApplyDocumentBackground(documentBackground);
        }
        private void InitializeDefaultValues()
        {
            DefaultFontSize = 16.0;
            DefaultFontStyle = System.Windows.FontStyles.Normal;
            DefaultFontFamily = FontFamilyes.FirstOrDefault(f => f != null && f.Source == "Consolas") ?? FontFamilyes[0];
            DefaultFontColor = appearanceDefaults.TextColor;
            DefaultFontBackground = FontColors.FirstOrDefault(c => c.Name == "Transparent");
            storedDocumentBackground =
                documentBackgroundPreferenceStore.Load();
            DocumentBackground =
                storedDocumentBackground ??
                appearanceDefaults.PaperColor;
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
                    // Нейтральные тона.
                    Drawing.Color.Black,
                    Drawing.Color.DimGray,
                    Drawing.Color.Gray,
                    Drawing.Color.DarkGray,
                    Drawing.Color.Silver,
                    Drawing.Color.LightGray,
                    Drawing.Color.Gainsboro,
                    Drawing.Color.White,

                    // Красные и тёплые тона.
                    Drawing.Color.Maroon,
                    Drawing.Color.DarkRed,
                    Drawing.Color.Red,
                    Drawing.Color.Crimson,
                    Drawing.Color.IndianRed,
                    Drawing.Color.Salmon,
                    Drawing.Color.LightCoral,
                    Drawing.Color.OrangeRed,
                    Drawing.Color.Orange,
                    Drawing.Color.Gold,
                    Drawing.Color.Yellow,

                    // Зелёные тона.
                    Drawing.Color.Olive,
                    Drawing.Color.DarkGreen,
                    Drawing.Color.Green,
                    Drawing.Color.SeaGreen,
                    Drawing.Color.LimeGreen,
                    Drawing.Color.YellowGreen,
                    Drawing.Color.Lime,

                    // Голубые и синие тона.
                    Drawing.Color.Teal,
                    Drawing.Color.DarkCyan,
                    Drawing.Color.Cyan,
                    Drawing.Color.Turquoise,
                    Drawing.Color.LightBlue,
                    Drawing.Color.SteelBlue,
                    Drawing.Color.Blue,
                    Drawing.Color.Navy,

                    // Фиолетовые и розовые тона.
                    Drawing.Color.Indigo,
                    Drawing.Color.Purple,
                    Drawing.Color.Magenta,
                    Drawing.Color.DeepPink,
                    Drawing.Color.Pink,

                    // Земляные тона.
                    Drawing.Color.Brown,
                    Drawing.Color.SaddleBrown,
                    Drawing.Color.Chocolate,
                    Drawing.Color.Tan,
                    Drawing.Color.Beige,

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
        private void ApplyCharacterProperty(DependencyProperty property, object? value)
        {
            Service.RestoreSelection();

            if(UsesWholeDocumentFormatting)
            {
                var documentRange = new TextRange(
                    Service.Document.ContentStart,
                    Service.Document.ContentEnd);
                documentRange.ApplyPropertyValue(property, value);
                return;
            }

            if(Service.Selection.IsEmpty)
                SetTypingProperty(property, value);
            else
                Service.Selection.ApplyPropertyValue(property, value);
        }

        private bool UsesWholeDocumentFormatting =>
            documentSession?.Template is { PreservesTextFormatting: false };

        private static Media.SolidColorBrush CreateBrush(Drawing.Color color) =>
            new(Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        private bool ApplyDocumentBackground(Drawing.Color color)
        {
            Media.Color mediaColor = Media.Color.FromArgb(
                color.A,
                color.R,
                color.G,
                color.B);
            if(Service.Document.Background is Media.SolidColorBrush existing &&
               existing.Color == mediaColor)
            {
                Service.BackGround = existing;
                DocumentBackground = color;
                return false;
            }

            var brush = CreateBrush(color);
            Service.Document.Background = brush;
            Service.BackGround = brush;
            DocumentBackground = color;
            return true;
        }

        private static Drawing.Color? GetDrawingColor(Media.Brush? brush) =>
            brush is Media.SolidColorBrush solid
                ? Drawing.Color.FromArgb(
                    solid.Color.A,
                    solid.Color.R,
                    solid.Color.G,
                    solid.Color.B)
                : null;
        private void NotifyDocumentBackgroundChanged()
        {
            Service.Service.InvalidateVisual();
            DocumentBackgroundChanged?.Invoke(this, EventArgs.Empty);
        }
        private void SetTypingProperty(DependencyProperty property, object? value)
        {
            Service.Focus();
            Service.SetTypingProperty(property, value);
        }
    }
}
