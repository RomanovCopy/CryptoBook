using CryptoBook.Infrastructure;

using System.Windows.Input;

using Drawing = System.Drawing;

namespace CryptoBook.ViewModels;

public sealed class InkPaletteSlotViewModel: ViewModelBase
{
    private readonly Action<Drawing.Color> applyColor;
    private readonly Action paletteChanged;
    private Drawing.Color color;

    public InkPaletteSlotViewModel(
        Drawing.Color color,
        IReadOnlyList<Drawing.Color> availableColors,
        Action<Drawing.Color> applyColor,
        Action paletteChanged)
    {
        this.color = color;
        this.applyColor = applyColor ??
            throw new ArgumentNullException(nameof(applyColor));
        this.paletteChanged = paletteChanged ??
            throw new ArgumentNullException(nameof(paletteChanged));
        ArgumentNullException.ThrowIfNull(availableColors);

        Choices = availableColors
            .Select(availableColor =>
                new InkPaletteColorChoiceViewModel(
                    availableColor,
                    ChangeColor))
            .ToArray();
    }

    public Drawing.Color Color
    {
        get => color;
        private set
        {
            if(color.ToArgb() == value.ToArgb())
                return;

            color = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToolTipText));
        }
    }

    public string ToolTipText => InkPaletteColorDisplay.Format(Color, Choices);

    public IReadOnlyList<InkPaletteColorChoiceViewModel> Choices { get; }

    public ICommand ApplyCommand => applyCommand ??=
        new RelayCommand(_ => applyColor(Color));
    private RelayCommand? applyCommand;

    private void ChangeColor(Drawing.Color value)
    {
        if(Color.ToArgb() == value.ToArgb())
            return;

        Color = value;
        paletteChanged();
    }
}

public sealed class InkPaletteColorChoiceViewModel
{
    public InkPaletteColorChoiceViewModel(
        Drawing.Color color,
        Action<Drawing.Color> chooseColor)
    {
        Color = color;
        ToolTipText = InkPaletteColorDisplay.Format(color);
        ChooseCommand = new RelayCommand(_ => chooseColor(color));
    }

    public Drawing.Color Color { get; }

    public string ToolTipText { get; }

    public ICommand ChooseCommand { get; }
}

internal static class InkPaletteColorDisplay
{
    public static string Format(
        Drawing.Color color,
        IReadOnlyList<InkPaletteColorChoiceViewModel>? choices = null)
    {
        Drawing.Color namedColor = choices?
            .Select(choice => choice.Color)
            .FirstOrDefault(candidate =>
                candidate.ToArgb() == color.ToArgb()) ?? color;
        string hexadecimal = $"#{unchecked((uint)color.ToArgb()):X8}";

        return namedColor.IsKnownColor
            ? $"{namedColor.Name} — {hexadecimal}"
            : hexadecimal;
    }
}
