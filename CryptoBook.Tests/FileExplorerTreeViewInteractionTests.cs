using CryptoBook.Behaviors;
using CryptoBook.DTO;
using CryptoBook.Models;

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileExplorerTreeViewInteractionTests
{
    [WpfTheory]
    [InlineData(1, MouseButton.Left)]
    [InlineData(2, MouseButton.Right)]
    [InlineData(3, MouseButton.Left)]
    public void NonDoubleLeftClick_DoesNotSuppressTreeInteraction(
        int clickCount,
        MouseButton button)
    {
        var row = CreateTreeRow(out _, out _);

        Assert.False(
            FileExplorerTreeInteractionBehavior
                .ShouldSuppressHeaderDoubleClick(row, button, clickCount));
    }

    [WpfFact]
    public void DoubleClickOnHeader_IsSuppressed()
    {
        var row = CreateTreeRow(out _, out _);

        Assert.True(
            FileExplorerTreeInteractionBehavior
                .ShouldSuppressHeaderDoubleClick(
                    row,
                    MouseButton.Left,
                    clickCount: 2));
    }

    [WpfFact]
    public void DoubleClickOnExpander_RemainsAvailable()
    {
        CreateTreeRow(out ToggleButton expander, out _);

        Assert.False(
            FileExplorerTreeInteractionBehavior
                .ShouldSuppressHeaderDoubleClick(
                    expander,
                    MouseButton.Left,
                    clickCount: 2));
    }

    [Fact]
    public void HoverTrigger_IsScopedToCurrentRow()
    {
        XDocument styles = XDocument.Load(FindRepositoryFile(
            "CryptoBook",
            "Styles",
            "TreeViewStyles.xaml"));
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XElement hoverTrigger = Assert.Single(
            styles.Descendants(presentation + "Trigger"),
            trigger =>
                (string?)trigger.Attribute("Property") == "IsMouseOver" &&
                (string?)trigger.Attribute("Value") == "True" &&
                trigger.Elements(presentation + "Setter").Any(setter =>
                    (string?)setter.Attribute("TargetName") == "Row" &&
                    (string?)setter.Attribute("Property") == "Background"));

        Assert.Equal("Row", (string?)hoverTrigger.Attribute("SourceName"));
    }

    [WpfFact]
    public void ExpandedAndCollapsedStates_ControlChildrenVisibility()
    {
        using FileStream stream = File.OpenRead(FindRepositoryFile(
            "CryptoBook",
            "Styles",
            "TreeViewStyles.xaml"));
        ResourceDictionary styles = Assert.IsType<ResourceDictionary>(
            XamlReader.Load(stream));
        var child = new TreeViewItem { Header = "Child" };
        var item = new TreeViewItem
        {
            Header = "Parent",
            Items = { child },
            Style = Assert.IsType<Style>(
                styles["DiskTree_TreeViewItemStyle"])
        };

        item.ApplyTemplate();
        item.UpdateLayout();
        ItemsPresenter presenter = Assert.IsType<ItemsPresenter>(
            item.Template.FindName("ItemsHost", item));

        item.IsExpanded = true;
        item.UpdateLayout();
        Assert.Equal(Visibility.Visible, presenter.Visibility);

        item.IsExpanded = false;
        item.UpdateLayout();
        Assert.Equal(Visibility.Collapsed, presenter.Visibility);
    }

    [Fact]
    public void FileExplorerTree_AttachesInteractionGuard()
    {
        string xaml = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Views",
            "FileExplorer.xaml"));

        Assert.Contains(
            "<behaviors:FileExplorerTreeInteractionBehavior/>",
            xaml);
    }

    [Fact]
    public void ProgrammaticSelectionOfCurrentNode_DoesNotNavigateAgain()
    {
        var current = CreateDirectory(@"C:\Work");

        Assert.True(FileExplorerModel.IsRedundantTreeSelection(
            current,
            current,
            @"C:\Work\",
            isCurrentDirectoryUnavailable: false));
        Assert.False(FileExplorerModel.IsRedundantTreeSelection(
            current,
            CreateDirectory(@"C:\Other"),
            @"C:\Work",
            isCurrentDirectoryUnavailable: false));
        Assert.False(FileExplorerModel.IsRedundantTreeSelection(
            current,
            current,
            @"C:\Other",
            isCurrentDirectoryUnavailable: false));
        Assert.False(FileExplorerModel.IsRedundantTreeSelection(
            current,
            current,
            @"C:\Work",
            isCurrentDirectoryUnavailable: true));
    }

    private static Border CreateTreeRow(
        out ToggleButton expander,
        out TreeViewItem item)
    {
        var row = new Border();
        expander = new ToggleButton();
        var layout = new StackPanel();
        layout.Children.Add(expander);
        layout.Children.Add(row);
        item = new TreeViewItem
        {
            Header = layout
        };
        var tree = new TreeView
        {
            Items = { item }
        };
        var host = new Border
        {
            Child = tree
        };
        host.Measure(new Size(400, 300));
        host.Arrange(new Rect(0, 0, 400, 300));
        host.UpdateLayout();
        return row;
    }

    private static DirectoryItem CreateDirectory(string fullPath) =>
        new(null!, null!, null!, null!)
        {
            FullPath = fullPath,
            Name = Path.GetFileName(fullPath)
        };

    private static string FindRepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while(directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. parts]);
            if(File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            string.Join(Path.DirectorySeparatorChar, parts));
    }
}
