using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ComboBoxLocalizationTests
{
    [WpfFact]
    public void DropUpTemplate_DisplaysSelectedLanguageWithThemeColors()
    {
        ResourceDictionary styles = LoadCompiledDictionary(
            "ComboBoxStyles.xaml");
        var foreground = new SolidColorBrush(Color.FromRgb(241, 243, 245));
        var background = new SolidColorBrush(Color.FromRgb(43, 45, 49));
        var border = new SolidColorBrush(Color.FromRgb(75, 85, 99));
        var comboBox = new ComboBox
        {
            Style = Assert.IsType<Style>(styles["DropUpComboBoxStyle"]),
            Foreground = foreground,
            Background = background,
            BorderBrush = border,
            Width = 220,
            Height = 34,
            ItemsSource = new[] { "English", "Русский" },
            SelectedIndex = 1
        };

        comboBox.ApplyTemplate();
        comboBox.Measure(new Size(220, 34));
        comboBox.Arrange(new Rect(0, 0, 220, 34));
        comboBox.UpdateLayout();

        var toggleButton = Assert.IsType<ToggleButton>(
            comboBox.Template.FindName("ToggleButton", comboBox));
        TextBlock selectedText = Assert.Single(
            FindVisualDescendants<TextBlock>(toggleButton),
            textBlock => textBlock.Text == "Русский");

        Assert.Equal(foreground.Color, GetColor(selectedText.Foreground));
        Assert.Equal(background.Color, GetColor(toggleButton.Background));
        Assert.Equal(
            HorizontalAlignment.Stretch,
            toggleButton.HorizontalContentAlignment);
    }

    [WpfFact]
    public void DropUpTemplate_HonorsDisplayMemberPathForSelectedItem()
    {
        ResourceDictionary styles = LoadCompiledDictionary(
            "ComboBoxStyles.xaml");
        var foreground = new SolidColorBrush(Color.FromRgb(241, 243, 245));
        var comboBox = new ComboBox
        {
            Style = Assert.IsType<Style>(styles["DropUpComboBoxStyle"]),
            Foreground = foreground,
            Background = Brushes.Black,
            BorderBrush = Brushes.Gray,
            Width = 220,
            Height = 34,
            DisplayMemberPath = nameof(FileTemplateOption.DisplayName),
            ItemsSource = new[] { new FileTemplateOption("PDF") },
            SelectedIndex = 0
        };

        comboBox.ApplyTemplate();
        comboBox.Measure(new Size(220, 34));
        comboBox.Arrange(new Rect(0, 0, 220, 34));
        comboBox.UpdateLayout();

        var toggleButton = Assert.IsType<ToggleButton>(
            comboBox.Template.FindName("ToggleButton", comboBox));
        TextBlock selectedText = Assert.Single(
            FindVisualDescendants<TextBlock>(toggleButton),
            textBlock => textBlock.Text == "PDF");

        Assert.Equal(foreground.Color, GetColor(selectedText.Foreground));
    }

    private static IEnumerable<T> FindVisualDescendants<T>(
        DependencyObject parent)
        where T: DependencyObject
    {
        for(int index = 0;
            index < VisualTreeHelper.GetChildrenCount(parent);
            index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if(child is T match)
                yield return match;

            foreach(T descendant in FindVisualDescendants<T>(child))
                yield return descendant;
        }
    }

    private static Color GetColor(Brush brush) =>
        Assert.IsType<SolidColorBrush>(brush).Color;

    private static ResourceDictionary LoadCompiledDictionary(string resourceName)
    {
        string resourcePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestAssets",
            resourceName);
        using Stream stream = File.OpenRead(resourcePath);

        return Assert.IsType<ResourceDictionary>(XamlReader.Load(stream));
    }

    private sealed record FileTemplateOption(string DisplayName);
}
