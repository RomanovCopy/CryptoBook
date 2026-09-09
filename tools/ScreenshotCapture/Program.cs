using Autofac;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Views;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length != 1)
        {
            Console.Error.WriteLine("Usage: ScreenshotCapture <repository-root>");
            return 2;
        }
        string root = Path.GetFullPath(args[0]);
        string output = Path.Combine(root, "docs", "screenshots");
        Directory.CreateDirectory(Path.Combine(output, "demo"));
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
        // Use the production XAML and container without App's migration, activation,
        // drive monitoring or recovery startup. The helper has its own settings profile.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        foreach(var dictionary in XDocument.Load(Path.Combine(root, "CryptoBook", "App.xaml"))
                    .Descendants(ns + "ResourceDictionary").Where(x => x.Attribute("Source") != null))
        {
            string path = dictionary.Attribute("Source")!.Value.TrimStart('/');
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/CryptoBook;component/{path}", UriKind.Relative)
            });
        }
        app.Resources["CurrentMargin"] = new Thickness(10, 2, 10, 2);
        app.Resources["Res"] = new ResourceWrapper();
        var container = new Startup().ConfigureServices(app);
        int result = 1;
        app.Startup += async (_, _) =>
        {
            try
            {
                await CaptureAsync(container, app, output);
                result = 0;
            }
            catch(Exception exception) { Console.Error.WriteLine(exception); }
            // Do not persist demonstration window/theme settings on normal close.
            Environment.Exit(result);
        };
        app.Run();
        return result;
    }

    private static async Task CaptureAsync(IContainer container, Application app, string output)
    {
        using var theme = new ThemeManager(app, new MemoryTheme(), new WindowsThemeProvider());
        theme.ApplyTheme(ApplicationTheme.Light);
        var manager = container.Resolve<IWindowManager>();
        var id = manager.CreateWindow<MainWindow>();
        var host = manager.FindHostWindow(id)!;
        var window = host.Window;
        window.WindowState = WindowState.Normal;
        window.Width = 1440;
        window.Height = 960;
        window.Left = 30;
        window.Top = 30;
        window.ShowActivated = false;
        string picture = Path.Combine(output, "demo", "mountain-lake.png");
        DrawLandscape(picture);
        var document = CreateDocument(picture);
        string documentPath = Path.Combine(output, "demo", "Альпийский маршрут.XamlPackage");
        using(var stream = File.Create(documentPath))
            new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.XamlPackage);
        var session = host.Scope.Resolve<IDocumentSession>();
        session.Open(documentPath, new XamlPackageFileTemplate(), document);
        manager.ShowWindow(id);
        await Settle(window);
        Save(window, Path.Combine(output, "editor.png"));

        host.Scope.Resolve<IMainWindowModel>().IsMenuOpen = true;
        await Settle(window);
        Save(window, Path.Combine(output, "side-menu.png"));
        host.Scope.Resolve<IMainWindowModel>().IsMenuOpen = false;

        theme.ApplyTheme(ApplicationTheme.Sepia);
        ViewModel<IRichtextboxViewModel>(window).ToggleView.Execute(null);
        await Settle(window);
        if(!ViewModel<IRichtextboxViewModel>(window).IsPreviewMode)
            throw new InvalidOperationException("Rich-text preview did not open.");
        Save(window, Path.Combine(output, "sepia-reading.png"));
        ViewModel<IRichtextboxViewModel>(window).ToggleView.Execute(null);

        theme.ApplyTheme(ApplicationTheme.Dark);
        string markdown = """
            # Альпийский маршрут

            **Полевой дневник · сентябрь 2026**

            ![Горное озеро на рассвете](mountain-lake.png)

            ## План путешествия

            | День | Маршрут | Расстояние |
            | --- | --- | --- |
            | 01 | Долина и озеро | 8 км |
            | 02 | Тропа к перевалу | 12 км |
            | 03 | Панорамный маршрут | 6 км |

            > Сохраните карту и заметки локально — они будут под рукой без интернета.

            ### Что взять с собой

            - Камеру и запасной аккумулятор
            - Карту маршрута и полевой дневник
            - Термос, дождевик и удобную обувь
            """;
        string markdownPath = Path.Combine(output, "demo", "Полевой дневник.md");
        File.WriteAllText(markdownPath, markdown, new UTF8Encoding(false));
        var markdownDocument = new FlowDocument(new Paragraph(new Run(markdown)));
        typeof(DocumentSession).Assembly.GetType("CryptoBook.Services.MarkdownDocumentMetadata")!
            .GetMethod("SetSource", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [markdownDocument, new MarkdownTextDocument(markdown, new UTF8Encoding(false), [])]);
        session.Open(markdownPath, new MarkdownFileTemplate(), markdownDocument);
        window.Width = 1120;
        await Settle(window);
        var markdownVm = ViewModel<IMarkdownEditorViewModel>(window);
        markdownVm.ToggleView.Execute(null);
        await Settle(window);
        if(!markdownVm.IsPreviewMode || markdownVm.PreviewDocument is null)
            throw new InvalidOperationException("Markdown preview did not open.");
        Save(window, Path.Combine(output, "markdown-preview.png"));
        markdownVm.OpenSyntaxHelp.Execute(null);
        await Settle(window);
        Save(window, Path.Combine(output, "markdown-help.png"));
        Console.WriteLine("SCREENSHOT_CAPTURE: PASS (5 production WPF views)");
    }

    private static async Task Settle(Window window)
    {
        await Task.Delay(700);
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
    }

    private static T ViewModel<T>(DependencyObject parent) where T : class
    {
        if(parent is FrameworkElement { IsVisible: true, DataContext: T model })
            return model;
        for(int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            try { return ViewModel<T>(VisualTreeHelper.GetChild(parent, i)); }
            catch(InvalidOperationException) { }
        }
        throw new InvalidOperationException($"Visible view model {typeof(T).Name} not found.");
    }

    private static void Save(Visual visual, string path, int width = 0, int height = 0)
    {
        if(visual is FrameworkElement element)
        {
            width = (int)Math.Ceiling(element.ActualWidth);
            height = (int)Math.Ceiling(element.ActualHeight);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        Console.WriteLine($"Captured {Path.GetFileName(path)}: {width}x{height}");
    }

    private static FlowDocument CreateDocument(string picture)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            Foreground = Brushes.Black, Background = Brushes.White
        };
        DocumentPageLayout.Apply(document, DocumentPaperSize.A4, false);
        document.Blocks.Add(new Paragraph(new Run("ПОЛЕВОЙ ДНЕВНИК  /  СЕНТЯБРЬ 2026"))
        { FontSize = 11, Foreground = Brush("#44797B"), Margin = new Thickness(0, 0, 0, 8) });
        document.Blocks.Add(new Paragraph(new Run("Альпийский маршрут"))
        { FontSize = 26, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        document.Blocks.Add(new Paragraph(new Run("Три дня среди горных вершин, тихих озёр и лесных троп."))
        { FontSize = 13, Foreground = Brush("#52616C"), Margin = new Thickness(0, 0, 0, 12) });
        document.Blocks.Add(new BlockUIContainer(new Image
        {
            Source = new BitmapImage(new Uri(picture)), Width = 690, Height = 190, Stretch = Stretch.UniformToFill
        }) { Margin = new Thickness(0, 0, 0, 6) });
        document.Blocks.Add(new Paragraph(new Run("01 / Горное озеро на рассвете · иллюстрация маршрута"))
        { FontSize = 10, Foreground = Brush("#52616C"), Margin = new Thickness(0, 0, 0, 12) });
        document.Blocks.Add(new Paragraph(new Run("План путешествия"))
        { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        document.Blocks.Add(new Paragraph(new Run("День 1 — прогулка по долине и остановка у озера.\nДень 2 — подъём к перевалу и съёмка панорамы.\nДень 3 — лесная тропа и возвращение в лагерь."))
        { LineHeight = 20, Margin = new Thickness(0, 0, 0, 10) });
        document.Blocks.Add(new Paragraph(new Run("Заметки в дорогу"))
        { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        document.Blocks.Add(new Paragraph(new Run("Карта, фотографии и дневник — в одном документе. Сохраняйте впечатления после каждого дня."))
        { LineHeight = 20, Margin = new Thickness(0) });
        return document;
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private static void DrawLandscape(string path)
    {
        // Original vector illustration rendered locally; no external photography.
        var visual = new DrawingVisual();
        using(var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(174, 213, 226), Color.FromRgb(241, 229, 207), 90), null, new Rect(0, 0, 1200, 440));
            dc.DrawEllipse(Brush("#FFF3CE"), null, new Point(907, 93), 42, 42);
            void Shape(string geometry, string fill) => dc.DrawGeometry(Brush(fill), null, Geometry.Parse(geometry));
            Shape("M0,270 L135,122 220,213 392,48 581,260 701,102 900,272 1040,133 1200,279 1200,440 0,440 Z", "#739CAC");
            Shape("M255,174 L392,48 504,174 433,142 395,115 353,145 321,130 Z", "#E8EFF0");
            Shape("M620,191 L701,102 791,190 724,163 697,141 674,169 Z", "#D9E6E8");
            Shape("M0,291 L103,216 271,305 438,228 611,310 800,219 984,314 1125,241 1200,280 1200,440 0,440 Z", "#356875");
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(74, 142, 154), Color.FromRgb(149, 192, 190), 90), null, new Rect(0, 306, 1200, 134));
            Shape("M0,281 L172,319 301,440 0,440 Z", "#214F53");
            Shape("M1200,280 L1015,320 882,440 1200,440 Z", "#214F53");
            for(int i = 0; i < 9; i++)
            {
                double x = 20 + i * 23;
                double y = 235 + i * 11;
                dc.DrawGeometry(Brush("#153C43"), null, Geometry.Parse(FormattableString.Invariant($"M{x},{y - 58} L{x - 24},{y + 32} {x + 24},{y + 32} Z")));
            }
            for(int i = 0; i < 7; i++)
                dc.DrawLine(new Pen(Brush("#B6D5D1"), 2), new Point(445 + i * 17, 331 + i * 13), new Point(794 - i * 13, 331 + i * 13));
        }
        Save(visual, path, 1200, 440);
    }

    private sealed class MemoryTheme : IThemePreferenceStore
    {
        public ApplicationTheme Load() => ApplicationTheme.Light;
        public void Save(ApplicationTheme theme) { }
    }
}
