using CSharpMath.Rendering.FrontEnd;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;
using MathPath = CSharpMath.Rendering.FrontEnd.Path;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;
using Color = System.Windows.Media.Color;

namespace CryptoBook.Services;

/// <summary>Draws the managed math engine's glyph outlines with WPF, without a native graphics runtime.</summary>
internal sealed class MathDrawingCanvas : ICanvas
{
    private readonly DrawingContext context;
    private readonly Stack<int> savedTransforms = new();
    private int transforms;
    public float Width => 0;
    public float Height => 0;
    public DrawingColor DefaultColor { get; set; } = DrawingColor.Black;
    public DrawingColor? CurrentColor { get; set; }
    public PaintStyle CurrentStyle { get; set; }
    internal MathDrawingCanvas(DrawingContext context) => this.context = context;

    public MathPath StartNewPath() => new GlyphPath(this);
    public void DrawLine(float x1, float y1, float x2, float y2, float lineThickness)
        => context.DrawLine(new Pen(Brush(CurrentColor), lineThickness), new Point(x1, y1), new Point(x2, y2));
    public void StrokeRect(float left, float top, float width, float height)
        => context.DrawRectangle(null, new Pen(Brush(CurrentColor), 1), new System.Windows.Rect(left, top, width, height));
    public void FillRect(float left, float top, float width, float height)
        => context.DrawRectangle(Brush(CurrentColor), null, new System.Windows.Rect(left, top, width, height));
    public void Save() { savedTransforms.Push(transforms); transforms = 0; }
    public void Translate(float dx, float dy) { context.PushTransform(new TranslateTransform(dx, dy)); transforms++; }
    public void Scale(float sx, float sy) { context.PushTransform(new ScaleTransform(sx, sy)); transforms++; }
    public void Restore()
    {
        while(transforms > 0) { context.Pop(); transforms--; }
        transforms = savedTransforms.Pop();
    }

    private SolidColorBrush Brush(DrawingColor? color)
    {
        var value = color ?? DefaultColor;
        return new SolidColorBrush(Color.FromArgb(value.A, value.R, value.G, value.B));
    }

    private sealed class GlyphPath : MathPath
    {
        private readonly MathDrawingCanvas canvas;
        private readonly StreamGeometry geometry = new() { FillRule = FillRule.Nonzero };
        private readonly StreamGeometryContext path;
        private Point start;
        private bool disposed;
        private bool hasFigure;
        public override DrawingColor? Foreground { get; set; }

        internal GlyphPath(MathDrawingCanvas canvas) { this.canvas = canvas; path = geometry.Open(); }
        public override void MoveTo(float x0, float y0)
        {
            start = new Point(x0, y0);
            path.BeginFigure(start, true, false);
            hasFigure = true;
        }
        public override void LineTo(float x1, float y1) => path.LineTo(new Point(x1, y1), true, false);
        public override void Curve3(float x1, float y1, float x2, float y2)
            => path.QuadraticBezierTo(new Point(x1, y1), new Point(x2, y2), true, false);
        public override void Curve4(float x1, float y1, float x2, float y2, float x3, float y3)
            => path.BezierTo(new Point(x1, y1), new Point(x2, y2), new Point(x3, y3), true, false);
        public override void CloseContour()
        {
            if(hasFigure) path.LineTo(start, true, false);
            hasFigure = false;
        }
        public override void Dispose()
        {
            if(disposed) return;
            disposed = true;
            path.Close();
            geometry.Freeze();
            var brush = canvas.Brush(Foreground ?? canvas.CurrentColor);
            canvas.context.DrawGeometry(canvas.CurrentStyle == PaintStyle.Fill ? brush : null,
                canvas.CurrentStyle == PaintStyle.Stroke ? new Pen(brush, 1) : null, geometry);
        }
    }
}
