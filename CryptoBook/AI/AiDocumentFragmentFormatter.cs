using CryptoBook.Infrastructure;

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace CryptoBook.AI;

/// <summary>
/// Styles a newly rendered AI fragment for insertion into a document. This is
/// deliberately separate from chat/Markdown preview styling and must not be
/// applied to existing user content.
/// </summary>
internal static class AiDocumentFragmentFormatter
{
    public static void Apply(FlowDocument fragment, FlowDocument target)
    {
        // Translucent ink tints blend with the actual paper (including images),
        // rather than baking the application theme's opaque colors into a file.
        var ink = (target.Foreground ?? Brushes.Black).CloneCurrentValue();
        var surface = CreateTint(ink, 0.06);
        var border = CreateTint(ink, 0.32);
        var linkInk = CreateTint(ink, 0.95);
        fragment.Foreground = ink;
        fragment.Background = Brushes.Transparent;

        foreach(var element in new FlowDocumentWalker().Traverse(fragment))
        {
            element.ClearValue(TextElement.ForegroundProperty);
            // A subtly softer ink keeps links readable on the same paper.
            // It also differs from the inherited foreground, so TextRange
            // preserves it instead of restoring WPF's default blue on load.
            if(element is Hyperlink)
                element.Foreground = linkInk;
            if(element is Inline)
            {
                // Inline code keeps its font; no background patch is needed.
                element.ClearValue(TextElement.BackgroundProperty);
            }
            else if(element.ReadLocalValue(TextElement.BackgroundProperty) != DependencyProperty.UnsetValue)
            {
                element.Background = surface;
            }

            if(element is Block block && block.ReadLocalValue(Block.BorderBrushProperty) != DependencyProperty.UnsetValue)
                block.BorderBrush = border;
            if(element is TableCell cell && cell.ReadLocalValue(TableCell.BorderBrushProperty) != DependencyProperty.UnsetValue)
                cell.BorderBrush = border;
        }
    }

    private static Brush CreateTint(Brush ink, double opacity)
    {
        // WPF's text serializer compares SolidColorBrush.Color, ignoring its
        // Opacity. Encode transparency in the color to preserve link overrides.
        if(ink is SolidColorBrush solid)
        {
            var color = solid.Color;
            color.A = (byte)Math.Round(color.A * solid.Opacity * opacity);
            var result = new SolidColorBrush(color);
            result.Freeze();
            return result;
        }
        var tint = ink.CloneCurrentValue();
        tint.Opacity *= opacity;
        if(tint.CanFreeze)
            tint.Freeze();
        return tint;
    }
}
