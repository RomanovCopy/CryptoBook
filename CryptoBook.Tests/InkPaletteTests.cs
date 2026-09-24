using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

using Drawing = System.Drawing;
using Media = System.Windows.Media;

using Xunit;

namespace CryptoBook.Tests;

public sealed class InkPaletteTests
{
    [Fact]
    public void Serializer_InvalidOrMissingSlots_UsesDefaults()
    {
        IReadOnlyList<Drawing.Color> colors =
            InkPaletteSerializer.Parse("#FF010203;invalid;#FF112233");

        Assert.Equal(InkPaletteSerializer.SlotCount, colors.Count);
        Assert.Equal(Drawing.Color.FromArgb(255, 1, 2, 3).ToArgb(), colors[0].ToArgb());
        Assert.Equal(Drawing.Color.DarkRed.ToArgb(), colors[1].ToArgb());
        Assert.Equal(Drawing.Color.FromArgb(255, 17, 34, 51).ToArgb(), colors[2].ToArgb());
        Assert.Equal(Drawing.Color.Purple.ToArgb(), colors[5].ToArgb());
    }

    [Fact]
    public void Serializer_RoundTripsArgbValues()
    {
        Drawing.Color[] source =
        [
            Drawing.Color.FromArgb(255, 1, 2, 3),
            Drawing.Color.FromArgb(255, 4, 5, 6),
            Drawing.Color.FromArgb(255, 7, 8, 9),
            Drawing.Color.FromArgb(255, 10, 11, 12),
            Drawing.Color.FromArgb(255, 13, 14, 15),
            Drawing.Color.FromArgb(255, 16, 17, 18)
        ];

        IReadOnlyList<Drawing.Color> restored =
            InkPaletteSerializer.Parse(
                InkPaletteSerializer.Serialize(source));

        Assert.Equal(
            source.Select(color => color.ToArgb()),
            restored.Select(color => color.ToArgb()));
    }

    [Fact]
    public void LeftClick_AppliesSlotColorWithoutChangingPalette()
    {
        var store = new RecordingInkPaletteStore([Drawing.Color.DarkRed]);
        var fontService = new RecordingFontService();
        var viewModel = new InkPaletteViewModel(store, fontService);

        viewModel.Colors[0].ApplyCommand.Execute(null);

        Assert.Equal(Drawing.Color.DarkRed.ToArgb(), fontService.AppliedColor?.ToArgb());
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void ColorChoice_ChangesSlotAndPersistsWithoutApplyingInk()
    {
        var store = new RecordingInkPaletteStore([Drawing.Color.Black]);
        var fontService = new RecordingFontService();
        var viewModel = new InkPaletteViewModel(store, fontService);
        InkPaletteColorChoiceViewModel choice = viewModel.Colors[0].Choices
            .Single(item => item.Color.ToArgb() == Drawing.Color.Blue.ToArgb());

        choice.ChooseCommand.Execute(null);

        Assert.Equal(Drawing.Color.Blue.ToArgb(), viewModel.Colors[0].Color.ToArgb());
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(Drawing.Color.Blue.ToArgb(), store.SavedColors[0].ToArgb());
        Assert.Null(fontService.AppliedColor);
    }

    private sealed class RecordingInkPaletteStore(
        IReadOnlyList<Drawing.Color> colors): CryptoBook.Interfaces.IInkPaletteStore
    {
        public int SaveCount { get; private set; }
        public IReadOnlyList<Drawing.Color> SavedColors { get; private set; } = [];

        public IReadOnlyList<Drawing.Color> Load() => colors;

        public void Save(IReadOnlyList<Drawing.Color> savedColors)
        {
            SaveCount++;
            SavedColors = savedColors.ToArray();
        }
    }

    private sealed class RecordingFontService: CryptoBook.Interfaces.IFontService
    {
        public Drawing.Color? AppliedColor { get; private set; }

        public event EventHandler? DocumentBackgroundChanged
        {
            add { }
            remove { }
        }

        public CryptoBook.Interfaces.IRichTextBoxService Service { get; set; } = null!;
        public double DefaultFontSize { get; set; }
        public FontStyle DefaultFontStyle { get; set; }
        public Media.FontFamily DefaultFontFamily { get; set; } = null!;
        public Drawing.Color DefaultFontColor { get; set; }
        public Drawing.Color DefaultFontBackground { get; set; }
        public Drawing.Color DocumentBackground { get; private set; }
        public bool HasDocumentBackgroundImage => false;
        public CryptoBook.Infrastructure.TextDecorationItem DefaultTextDecoration { get; set; } = null!;
        public FontWeight DefaultFontWeight { get; set; }
        public FontStretch DefaultFontStretch { get; set; }
        public ObservableCollection<double> FontSizes { get; set; } = [];
        public ObservableCollection<FontStyle> FontStyles { get; set; } = [];
        public ObservableCollection<Media.FontFamily> FontFamilyes { get; set; } = [];
        public ObservableCollection<Drawing.Color> FontColors { get; set; } =
        [
            Drawing.Color.Black,
            Drawing.Color.DarkRed,
            Drawing.Color.Blue
        ];
        public ObservableCollection<CryptoBook.Infrastructure.TextDecorationItem> TextDecorations { get; set; } = [];
        public ObservableCollection<FontWeight> FontWeights { get; set; } = [];
        public ObservableCollection<FontStretch> FontStretches { get; set; } = [];

        public void SetFontColor(Drawing.Color? fontColor) =>
            AppliedColor = fontColor;

        public void SetFontStyle(FontStyle? fontStyle) { }
        public void SetFontWeight(FontWeight? fontWeight) { }
        public void SetFontStretch(FontStretch? fontStretch) { }
        public void SetFontFamily(Media.FontFamily? fontFamily) { }
        public void SetTextDecoration(TextDecorationCollection decoration) { }
        public void SetFontBackground(Drawing.Color? fontBackground) { }
        public void SetDocumentBackground(Drawing.Color? documentBackground) { }
        public void SetDocumentBackgroundImage(BitmapSource backgroundImage) { }
        public void SetDocumentBackgroundImageCrop(Rect crop) { }
        public void CommitDocumentBackgroundImage(Media.ImageBrush expected, Media.ImageBrush saved) { }
        public void ClearDocumentBackgroundImage() { }
        public void SetFontSize(double fontSize) { }
        public void ClearFormatting() { }
    }
}
