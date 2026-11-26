using CSharpMath.Atom;
using CSharpMath.Atom.Atoms;
using CSharpMath.Rendering.FrontEnd;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfImage = System.Windows.Controls.Image;
using WpfInline = System.Windows.Documents.Inline;
using DrawingColor = System.Drawing.Color;
using Brush = System.Windows.Media.Brush;
using Binding = System.Windows.Data.Binding;
using Pen = System.Windows.Media.Pen;

namespace CryptoBook.Services;

/// <summary>Typesets LaTeX as local WPF vector drawings; invalid or incomplete input stays visible as source.</summary>
internal static class MarkdownMathRenderer
{
    private const double BaseFontSize = 15;
    private const double FormulaScale = 1.3;
    private static readonly FontSizeConverter SizeConverter = new();
    private static readonly ViewportWidthConverter ViewportConverter = new();

    static MarkdownMathRenderer()
    {
        AddArrowCommand(@"\xrightarrow", "⟶");
        AddArrowCommand(@"\xleftarrow", "⟵");
    }

    private static void AddArrowCommand(string command, string symbol)
        => LaTeXSettings.Commands.Add(command, (parser, _, _) =>
            parser.ReadArgumentOptional().Bind(below => parser.ReadArgument().Bind(above =>
            {
                var arrow = new LargeOperator(symbol, true);
                foreach(var atom in above) arrow.Superscript.Add(atom);
                if(below is not null)
                    foreach(var atom in below) arrow.Subscript.Add(atom);
                return LaTeXSettings.Ok(arrow);
            })));

    internal static WpfInline Render(string latex, string source, bool display, bool complete = true)
    {
        if(!complete || !WithinLimits(latex)) return new Run(source);
        try
        {
            var painter = new FormulaPainter
            {
                LaTeX = latex,
                FontSize = (float)(BaseFontSize * 72 / 96),
                LineStyle = display ? LineStyle.Display : LineStyle.Text,
                DisplayErrorInline = false
            };
            if(painter.ErrorMessage is not null) return new Run(source);
            var drawing = new DrawingGroup();
            using(var context = drawing.Open()) painter.Draw(new MathDrawingCanvas(context), 0, 0);
            var bounds = drawing.Bounds;
            if(bounds.IsEmpty || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
               bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width > 20000 || bounds.Height > 10000)
                return new Run(source);

            var inline = new InlineUIContainer { BaselineAlignment = BaselineAlignment.Center };
            BindInk(drawing, inline);
            var image = new WpfImage
            {
                Source = new DrawingImage(drawing), Stretch = Stretch.Uniform,
                ToolTip = source, Tag = new FormulaSource(source)
            };
            AutomationProperties.SetName(image, source);
            image.SetBinding(FrameworkElement.WidthProperty, SizeBinding(inline, bounds.Width));
            image.SetBinding(FrameworkElement.MaxWidthProperty, new Binding(nameof(System.Windows.Controls.ScrollViewer.ViewportWidth))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(System.Windows.Controls.ScrollViewer), 1),
                Converter = ViewportConverter, FallbackValue = 900d
            });
            inline.Child = image;
            return inline;
        }
        catch(Exception error) when(error is not OutOfMemoryException)
        {
            // Model output is untrusted input to the typesetter, including unfinished commands.
            return new Run(source);
        }
    }

    // Bound the managed parser's recursion and the amount of work on the UI thread.
    private static bool WithinLimits(string latex)
    {
        if(latex.Length > 4096) return false;
        int depth = 0, operators = 0;
        for(int i = 0; i < latex.Length; i++)
        {
            char value = latex[i];
            // TeX also permits recursive arguments without braces (e.g. \sqrt\sqrt x).
            if((value is '\\' or '^' or '_') && ++operators > 128) return false;
            if(value == '\\' && i + 1 < latex.Length && !char.IsLetter(latex[i + 1])) { i++; continue; }
            if(value == '{' && ++depth > 64) return false;
            if(value == '}' && --depth < 0) return false;
        }
        return true;
    }

    private static Binding SizeBinding(InlineUIContainer inline, double size)
        => new(nameof(TextElement.FontSize)) { Source = inline, Converter = SizeConverter, ConverterParameter = size / BaseFontSize * FormulaScale };

    private static void BindInk(Drawing drawing, InlineUIContainer inline)
    {
        if(drawing is DrawingGroup group)
            foreach(var child in group.Children) BindInk(child, inline);
        if(drawing is not GeometryDrawing geometry) return;
        if(IsDefaultInk(geometry.Brush))
            BindingOperations.SetBinding(geometry, GeometryDrawing.BrushProperty, new Binding(nameof(TextElement.Foreground)) { Source = inline });
        if(geometry.Pen is { } pen && IsDefaultInk(pen.Brush))
            BindingOperations.SetBinding(pen, Pen.BrushProperty, new Binding(nameof(TextElement.Foreground)) { Source = inline });
    }

    private static bool IsDefaultInk(Brush? brush) => brush is SolidColorBrush solid && solid.Color == Colors.Black;

    /// <summary>Embed a sharp bitmap with the document's ink; no custom control or external asset is needed on reopen.</summary>
    internal static void PrepareForInsertion(WpfImage image, Brush foreground)
    {
        if(image.Tag is not FormulaSource || image.Source is not DrawingImage source) return;
        ApplyInk(source.Drawing, foreground);
        double width = Math.Min(image.Width, image.MaxWidth), height = width * source.Height / source.Width;
        if(!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return;
        double scale = Math.Min(3, Math.Sqrt(16000000 / (width * height)));
        var visual = new DrawingVisual();
        using(var context = visual.RenderOpen()) context.DrawImage(source, new Rect(0, 0, width, height));
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(width * scale)),
            Math.Max(1, (int)Math.Ceiling(height * scale)), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        // Resolve the inherited font size before the independent fragment is serialized.
        BindingOperations.ClearBinding(image, FrameworkElement.WidthProperty);
        BindingOperations.ClearBinding(image, FrameworkElement.HeightProperty);
        BindingOperations.ClearBinding(image, FrameworkElement.MaxWidthProperty);
        image.Width = width; image.Height = height;
        image.Source = bitmap;
    }

    private static void ApplyInk(Drawing drawing, Brush foreground)
    {
        if(drawing is DrawingGroup group)
            foreach(var child in group.Children) ApplyInk(child, foreground);
        if(drawing is not GeometryDrawing geometry) return;
        if(BindingOperations.IsDataBound(geometry, GeometryDrawing.BrushProperty))
        {
            BindingOperations.ClearBinding(geometry, GeometryDrawing.BrushProperty);
            geometry.Brush = foreground;
        }
        if(geometry.Pen is { } pen && BindingOperations.IsDataBound(pen, Pen.BrushProperty))
        {
            BindingOperations.ClearBinding(pen, Pen.BrushProperty);
            pen.Brush = foreground;
        }
    }

    private sealed record FormulaSource(string Source);
    private sealed class FontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (double)value * (double)parameter;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    private sealed class ViewportWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double width && double.IsFinite(width) && width > 24 ? width - 24 : 900d;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    private sealed class FormulaPainter : MathPainter<MathDrawingCanvas, DrawingColor>
    {
        public override DrawingColor WrapColor(DrawingColor color) => color;
        public override DrawingColor UnwrapColor(DrawingColor color) => color;
        public override ICanvas WrapCanvas(MathDrawingCanvas canvas) => canvas;
    }
}
