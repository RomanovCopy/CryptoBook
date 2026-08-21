using CryptoBook.Services;

using System.Windows;

using Point = System.Windows.Point;
using Size = System.Windows.Size;

using Xunit;

namespace CryptoBook.Tests;

public class WindowLayoutDefaultsTests
{
    [Fact]
    public void MainWindow_DefaultPlacementIsCenteredAndComfortable()
    {
        Rect placement = WindowLayoutDefaults.CreateMain(
            new Rect(0, 0, 1920, 1040));

        Assert.Equal(1200, placement.Width);
        Assert.Equal(800, placement.Height);
        Assert.Equal(360, placement.Left);
        Assert.Equal(120, placement.Top);
    }

    [Fact]
    public void Explorer_DefaultPlacementFitsSmallerWorkArea()
    {
        Rect placement = WindowLayoutDefaults.CreateExplorer(
            new Rect(0, 0, 1366, 728));

        Assert.Equal(1100, placement.Width);
        Assert.Equal(655.2, placement.Height, precision: 6);
        Assert.Equal(133, placement.Left);
        Assert.Equal(36.4, placement.Top, precision: 6);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(400, 100)]
    [InlineData(double.NaN, 800)]
    public void MainWindow_LegacyTinySizesAreReplaced(
        double width,
        double height)
    {
        Assert.True(WindowLayoutDefaults.IsLegacyMainSize(width, height));
    }

    [Fact]
    public void UserSizedMainWindowIsPreserved()
    {
        Assert.False(WindowLayoutDefaults.IsLegacyMainSize(900, 650));
    }

    [Fact]
    public void MediaPlayer_CascadesFromPreviousWindow()
    {
        var previous = new Rect(480, 220, 960, 600);

        Point placement = WindowLayoutDefaults.CreateMediaPlayerCascade(
            new Rect(0, 0, 1920, 1040),
            previous,
            new Size(960, 600),
            [previous]);

        Assert.Equal(new Point(516, 256), placement);
        Assert.False(Contains(previous, new Rect(placement, new Size(960, 600))));
    }

    [Fact]
    public void MediaPlayer_CascadeWrapsAtWorkAreaEdge()
    {
        var previous = new Rect(960, 440, 960, 600);

        Point placement = WindowLayoutDefaults.CreateMediaPlayerCascade(
            new Rect(0, 0, 1920, 1040),
            previous,
            new Size(960, 600),
            [previous]);

        Assert.Equal(new Point(36, 36), placement);
    }

    [Fact]
    public void MediaPlayer_CascadeAvoidsCompleteContainment()
    {
        var previous = new Rect(100, 100, 1200, 800);
        var size = new Size(680, 420);

        Point placement = WindowLayoutDefaults.CreateMediaPlayerCascade(
            new Rect(0, 0, 1920, 1040),
            previous,
            size,
            [previous]);

        Assert.False(Contains(previous, new Rect(placement, size)));
    }

    [Fact]
    public void FileExplorer_DefaultColumnsFavorNamesAndDates()
    {
        string? original = Properties.Settings.Default.GridViewColumnRatios;
        try
        {
            Properties.Settings.Default.GridViewColumnRatios = string.Empty;
            var store = new ColumnLayoutStoreService();

            bool loaded = store.TryLoad(
                "FileExplorer.MainGrid",
                out IReadOnlyList<double> ratios);

            Assert.True(loaded);
            Assert.Equal([0.46, 0.27, 0.15, 0.12], ratios);
        }
        finally
        {
            Properties.Settings.Default.GridViewColumnRatios = original;
        }
    }

    [Fact]
    public void FileExplorer_FlatViewDefaultColumnsFillAvailableWidth()
    {
        string? original = Properties.Settings.Default.GridViewColumnRatios;
        try
        {
            Properties.Settings.Default.GridViewColumnRatios = string.Empty;
            var store = new ColumnLayoutStoreService();

            bool loaded = store.TryLoad(
                "FileExplorer.MainGrid|Name,RelativeDirectory,LastWriteTimeUtc,Extension,Size",
                out IReadOnlyList<double> ratios);

            Assert.True(loaded);
            Assert.Equal([0.30, 0.28, 0.20, 0.12, 0.10], ratios);
            Assert.Equal(1d, ratios.Sum(), precision: 12);
        }
        finally
        {
            Properties.Settings.Default.GridViewColumnRatios = original;
        }
    }

    private static bool Contains(Rect outer, Rect inner) =>
        outer.Left <= inner.Left &&
        outer.Top <= inner.Top &&
        outer.Right >= inner.Right &&
        outer.Bottom >= inner.Bottom;
}
