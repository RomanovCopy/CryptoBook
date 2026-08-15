using CryptoBook.Services;

using System.Windows;

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
}
