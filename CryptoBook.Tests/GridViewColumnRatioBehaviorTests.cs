using CryptoBook.Behaviors;
using CryptoBook.Interfaces;

using Microsoft.Xaml.Behaviors;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class GridViewColumnRatioBehaviorTests
{
    [WpfFact]
    public async Task FirstLayout_ColumnsFillAvailableListWidth()
    {
        var gridView = new GridView();
        foreach(string tag in new[]
        {
            "Name",
            "RelativeDirectory",
            "LastWriteTimeUtc",
            "Extension",
            "Size"
        })
        {
            gridView.Columns.Add(new GridViewColumn
            {
                Header = new GridViewColumnHeader { Tag = tag },
                CellTemplate = new DataTemplate()
            });
        }

        var listView = new ListView
        {
            View = gridView,
            ItemsSource = Array.Empty<object>()
        };
        Interaction.GetBehaviors(listView).Add(
            new GridViewColumnRatioBehavior
            {
                ViewId = "FileExplorer.MainGrid",
                Store = new ColumnLayoutStoreStub(
                    [0.30, 0.28, 0.20, 0.12, 0.10]),
                MinColumnWidth = 60
            });
        var host = new Window
        {
            Width = 800,
            Height = 300,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Left = -10_000,
            Top = -10_000,
            Content = listView
        };

        try
        {
            host.Show();
            await listView.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.ContextIdle);
            listView.UpdateLayout();
            await listView.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.ContextIdle);

            double totalColumnWidth = gridView.Columns.Sum(
                column => column.Width);

            Assert.True(listView.ActualWidth > 0);
            Assert.InRange(
                totalColumnWidth,
                listView.ActualWidth * 0.90,
                listView.ActualWidth);
            Assert.All(
                gridView.Columns,
                column => Assert.True(column.Width >= 60));
        }
        finally
        {
            host.Close();
        }
    }

    private sealed class ColumnLayoutStoreStub(
        IReadOnlyList<double> ratios): IColumnLayoutStore
    {
        public bool TryLoad(
            string viewId,
            out IReadOnlyList<double> loadedRatios)
        {
            loadedRatios = ratios;
            return true;
        }

        public void Save(string viewId, IReadOnlyList<double> savedRatios)
        {
        }
    }
}
