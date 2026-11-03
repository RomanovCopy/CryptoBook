using CryptoBook.Infrastructure;

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

using Xunit;

namespace CryptoBook.Tests;

[Collection("Settings UI")]
public sealed class FileDeletionConfirmationLayoutTests
{
    [WpfTheory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    [InlineData("uk-UA")]
    [InlineData("de-DE")]
    public async Task LongPermanentDeletionWarningKeepsBothButtonsVisible(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "CryptoBook", "Views", "MessageWindow.xaml")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            XElement source = XDocument.Load(Path.Combine(directory.FullName, "CryptoBook", "Views", "MessageWindow.xaml")).Root!;
            XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            XNamespace infra = "clr-namespace:CryptoBook.Infrastructure;assembly=CryptoBook";
            var root = new XElement(ns + "UserControl",
                new XAttribute(XNamespace.Xmlns + "x", x),
                new XAttribute(XNamespace.Xmlns + "infra", infra),
                new XElement(ns + "UserControl.Resources",
                    new XElement(infra + "ResourceWrapper", new XAttribute(x + "Key", "Res")),
                    new XElement(ns + "BooleanToVisibilityConverter", new XAttribute(x + "Key", "BooleanVisibility"))),
                source.Element(ns + "Grid"));
            foreach(XAttribute visibility in root.Descendants().Attributes("Visibility"))
                visibility.Value = "{Binding IsCanceled, Converter={StaticResource BooleanVisibility}}";
            var view = (UserControl)XamlReader.Parse(root.ToString());
            bool? consent = null;
            string details = string.Join(Environment.NewLine, Enumerable.Range(0, 6).Select(index =>
                LocalizationManager.Format("Explorer.RecycleSpaceLine", $"{(char)('C' + index)}:\\", 12345.67, 12.34)));
            view.DataContext = new
            {
                Message = LocalizationManager.Format("Explorer.PermanentDeleteWholeSelectionWarning", 10000, details),
                IsCanceled = true,
                OkCommand = new RelayCommand(_ => consent = true),
                CancelCommand = new RelayCommand(_ => consent = false)
            };
            view.Background = Brushes.White;
            view.Width = 400; view.Height = 180;
            view.Measure(new Size(400, 180)); view.Arrange(new Rect(0, 0, 400, 180));
            await Dispatcher.Yield(DispatcherPriority.DataBind);
            view.UpdateLayout();
            Grid grid = Assert.IsType<Grid>(view.Content);
            ScrollViewer body = Assert.Single(grid.Children.OfType<ScrollViewer>());
            Assert.True(body.ScrollableHeight > 0);
            StackPanel footer = Assert.Single(grid.Children.OfType<StackPanel>());
            Button[] buttons = footer.Children.OfType<Button>().ToArray();
            Assert.Equal(2, buttons.Length);
            foreach(Button button in buttons)
            {
                Assert.True(button.IsVisible || button.Visibility == Visibility.Visible);
                Rect bounds = button.TransformToAncestor(view).TransformBounds(new Rect(button.RenderSize));
                Assert.InRange(bounds.Top, 0, view.ActualHeight);
                Assert.InRange(bounds.Bottom, 0, view.ActualHeight);
                Assert.NotNull(button.Command);
            }
            Assert.Equal(LocalizationManager.GetString("Common.Cancel"), buttons[1].Content);
            buttons[1].Command.Execute(null);
            Assert.False(consent);
            buttons[0].Command.Execute(null);
            Assert.True(consent);
            if(culture == "ru-RU")
            {
                string image = Path.Combine(directory.FullName, "artifacts", "FileExplorerProgress", "permanent-deletion-warning.png");
                Directory.CreateDirectory(Path.GetDirectoryName(image)!);
                var bitmap = new RenderTargetBitmap(400, 180, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(image); encoder.Save(stream);
            }
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
