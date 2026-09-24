using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Windows.Input;

using Drawing = System.Drawing;

namespace CryptoBook.ViewModels;

public sealed class InkPaletteViewModel:
    ViewModelBase,
    IInkPaletteViewModel
{
    private readonly IInkPaletteStore paletteStore;
    private readonly IFontService fontService;

    public InkPaletteViewModel(
        IInkPaletteStore paletteStore,
        IFontService fontService)
    {
        this.paletteStore = paletteStore ??
            throw new ArgumentNullException(nameof(paletteStore));
        this.fontService = fontService ??
            throw new ArgumentNullException(nameof(fontService));

        Drawing.Color[] availableColors = fontService.FontColors
            .Where(color => color.A > 0)
            .GroupBy(color => color.ToArgb())
            .Select(group => group.First())
            .ToArray();

        foreach(Drawing.Color color in paletteStore.Load())
        {
            Colors.Add(new InkPaletteSlotViewModel(
                color,
                availableColors,
                ApplyColor,
                SavePalette));
        }
    }

    public ObservableCollection<InkPaletteSlotViewModel> Colors { get; } = [];

    public ICommand Loaded => NoOpCommand;
    public ICommand Close => NoOpCommand;
    public ICommand Closing => NoOpCommand;
    public ICommand Closed => NoOpCommand;

    private void ApplyColor(Drawing.Color color) =>
        fontService.SetFontColor(color);

    private void SavePalette() =>
        paletteStore.Save(
            Colors.Select(slot => slot.Color).ToArray());

    private static ICommand NoOpCommand { get; } =
        new RelayCommand(_ => { });
}
