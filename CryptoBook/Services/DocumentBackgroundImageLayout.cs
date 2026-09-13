using System.Windows;
using System.Windows.Media;
using Size = System.Windows.Size;

namespace CryptoBook.Services;

/// <summary>Computes a movable source crop without exposing empty space at the paper edges.</summary>
internal static class DocumentBackgroundImageLayout
{
    public static readonly DependencyProperty IsFinalizedProperty = DependencyProperty.RegisterAttached(
        "IsFinalized", typeof(bool), typeof(DocumentBackgroundImageLayout), new PropertyMetadata(false));

    public static bool IsFinalized(ImageBrush image) => (bool)image.GetValue(IsFinalizedProperty);

    // The editor is a continuous sheet. Use a paper-sized minimum, never the window
    // height; longer content may extend the sheet, while view zoom must not recrop it.
    public static Size GetPaperSize(double pageWidth, double contentHeight = 0) =>
        new(pageWidth, Math.Max(pageWidth * Math.Sqrt(2), contentHeight));

    public static ImageBrush MapToPaper(ImageBrush image, Size paper)
    {
        if(IsFinalized(image))
            return image;
        var viewport = new Rect(paper);
        if(image.ViewportUnits == BrushMappingMode.Absolute && image.Viewport == viewport)
            return image;
        var mapped = image.CloneCurrentValue();
        mapped.ImageSource = image.ImageSource;
        mapped.ViewportUnits = BrushMappingMode.Absolute;
        mapped.Viewport = viewport;
        mapped.Freeze();
        return mapped;
    }

    public static bool IsValidCrop(Rect crop) =>
        !crop.IsEmpty && double.IsFinite(crop.X) && double.IsFinite(crop.Y) &&
        double.IsFinite(crop.Width) && double.IsFinite(crop.Height) &&
        crop.Width > 0 && crop.Height > 0 && crop.X >= 0 && crop.Y >= 0 &&
        crop.Right <= 1.0000001 && crop.Bottom <= 1.0000001;

    public static Rect GetVisibleCrop(ImageBrush brush, Size paper)
    {
        Rect crop = brush.Viewbox;
        if(brush.ViewboxUnits != BrushMappingMode.RelativeToBoundingBox || !IsValidCrop(crop))
            crop = new Rect(0, 0, 1, 1);
        double sourceWidth = brush.ImageSource.Width * crop.Width;
        double sourceHeight = brush.ImageSource.Height * crop.Height;
        double scale = Math.Max(paper.Width / sourceWidth, paper.Height / sourceHeight);
        double width = paper.Width / scale / brush.ImageSource.Width;
        double height = paper.Height / scale / brush.ImageSource.Height;
        double x = brush.AlignmentX switch { AlignmentX.Left => 0, AlignmentX.Right => 1, _ => 0.5 };
        double y = brush.AlignmentY switch { AlignmentY.Top => 0, AlignmentY.Bottom => 1, _ => 0.5 };
        return new Rect(crop.X + (crop.Width - width) * x, crop.Y + (crop.Height - height) * y, width, height);
    }

    public static Rect MoveCrop(Rect start, Size paper, Vector delta) => new(
        Math.Clamp(start.X - delta.X / paper.Width * start.Width, 0, Math.Max(0, 1 - start.Width)),
        Math.Clamp(start.Y - delta.Y / paper.Height * start.Height, 0, Math.Max(0, 1 - start.Height)),
        start.Width, start.Height);
}
