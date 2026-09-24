using CryptoBook.Interfaces;

using System.Globalization;

using Drawing = System.Drawing;

namespace CryptoBook.Services;

public sealed class UserInkPaletteStore: IInkPaletteStore
{
    public IReadOnlyList<Drawing.Color> Load() =>
        InkPaletteSerializer.Parse(
            Properties.Settings.Default.InkPaletteColors);

    public void Save(IReadOnlyList<Drawing.Color> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);

        Properties.Settings.Default.InkPaletteColors =
            InkPaletteSerializer.Serialize(colors);
        Properties.Settings.Default.Save();
    }
}

internal static class InkPaletteSerializer
{
    public const int SlotCount = 6;

    private static readonly Drawing.Color[] DefaultColors =
    [
        Drawing.Color.Black,
        Drawing.Color.DarkRed,
        Drawing.Color.Orange,
        Drawing.Color.DarkGreen,
        Drawing.Color.Blue,
        Drawing.Color.Purple
    ];

    public static IReadOnlyList<Drawing.Color> Parse(string? value)
    {
        string[] tokens = string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.TrimEntries);
        var colors = new Drawing.Color[SlotCount];

        for(int index = 0; index < SlotCount; index++)
        {
            colors[index] = index < tokens.Length &&
                            TryParseColor(tokens[index], out Drawing.Color color)
                ? color
                : DefaultColors[index];
        }

        return colors;
    }

    public static string Serialize(IReadOnlyList<Drawing.Color> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);

        var normalized = new Drawing.Color[SlotCount];
        for(int index = 0; index < SlotCount; index++)
        {
            normalized[index] = index < colors.Count
                ? colors[index]
                : DefaultColors[index];
        }

        return string.Join(
            ";",
            normalized.Select(color =>
                $"#{unchecked((uint)color.ToArgb()):X8}"));
    }

    private static bool TryParseColor(
        string value,
        out Drawing.Color color)
    {
        string hexadecimal = value.StartsWith('#')
            ? value[1..]
            : value;
        if(hexadecimal.Length == 8 &&
           uint.TryParse(
               hexadecimal,
               NumberStyles.AllowHexSpecifier,
               CultureInfo.InvariantCulture,
               out uint argb))
        {
            color = Drawing.Color.FromArgb(unchecked((int)argb));
            return true;
        }

        color = default;
        return false;
    }
}
