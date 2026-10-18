using Autofac;
using CryptoBook.AI;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Views;
using CryptoBook.ViewModels;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class UiPreview
{
    public static void Run(string modelPath, string outputDirectory)
    {
        var app = CreateApplication();
        RunPreview(app, modelPath, outputDirectory);
    }

    internal static Application CreateApplication()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources["Res"] = new CryptoBook.Infrastructure.ResourceWrapper();
        app.Resources["CurrentMargin"] = new Thickness(10, 2, 10, 2);
        foreach(string dictionary in new[] { "Styles/WindowStyles", "Styles/ControlStyles", "Styles/ImageStyles", "Themes/SystemTheme",
            "Styles/ListStyles", "Styles/ButtonStyles", "Styles/EditorToolbarIcons", "Styles/TreeViewStyles", "Styles/ComboBoxStyles", "Styles/ThemedControls" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
                { Source = new Uri("/CryptoBook;component/" + dictionary + ".xaml", UriKind.Relative) });
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CryptoBook;component/Themes/LightTheme.xaml", UriKind.Relative) });
        return app;
    }

    private static void RunPreview(Application app, string modelPath, string outputDirectory)
    {
        using var container = new Startup().ConfigureServices(app);
        var windows = container.Resolve<IWindowManager>();
        Guid id = windows.CreateWindow<MainWindow>();
        var main = (MainWindow)windows.FindHostWindow(id)!.Window;
        var scope = DiScope.GetScope(main)!;
        var vm = (MainWindowViewModel)main.DataContext;
        var ai = vm.Ai!;
        ai.IsOpen = true;
        ai.Messages.Add(new("user", "Сократи документ, сохрани бюджет и срок проверки.",
            [AiAttachmentService.Document("Бюджет проекта.txt", "Бюджет проекта — 120 000 рублей, проверка займёт три рабочих дня.")]));
        ai.Messages.Add(new("assistant", "Бюджет проекта — 120 000 рублей, проверка займёт три рабочих дня."));
        scope.Resolve<IPageNavigationService>().Navigate("Home");
        var document = new System.Windows.Documents.FlowDocument(new System.Windows.Documents.Paragraph(
            new System.Windows.Documents.Run("Бюджет проекта — 120 000 рублей. Проверка займёт три рабочих дня.")));
        var session = scope.Resolve<IDocumentSession>();
        session.Open("AI preview.txt", new CryptoBook.FileTemplates.PlainTextTemplate(), document);
        var richText = scope.Resolve<IRichTextBoxService>();
        richText.Selection.Select(document.ContentStart, document.ContentEnd);
        if(container.Resolve<AiEditorContext>().Capture()?.HasSelection != true)
            throw new Exception("AI context is not bound to the active main-window navigation scope.");
        // Render real WPF controls without invoking the main window's Loaded command or updater.
        Directory.CreateDirectory(outputDirectory);
        ai.AttachDocument.Execute(null);
        string imagePath = Path.GetFullPath(Path.Combine(outputDirectory, "sample.png"));
        AttachmentSmoke.CreateImage(imagePath);
        byte[] imageBytes = File.ReadAllBytes(imagePath);
        var preview = new BitmapImage();
        using(var imageStream = new MemoryStream(imageBytes))
        {
            preview.BeginInit(); preview.CacheOption = BitmapCacheOption.OnLoad; preview.StreamSource = imageStream; preview.EndInit(); preview.Freeze();
        }
        ai.Attachments.Add(new("sample.png", "", imageBytes, preview, imageBytes.Length));
        Render((FrameworkElement)main.Content, 1200, 800, Path.Combine(outputDirectory, "ai-panel-light.png"));
        VerifyPanelResizing(main, ai, outputDirectory);
        var settingsVm = new AiModelSettingsViewModel(container.Resolve<AiSettingsStore>(), container.Resolve<IAiInferenceService>())
        {
            ModelPath = modelPath,
            Enabled = true
        };
        var settings = new AiModelSettingsWindow(settingsVm);
        settings.Show();
        var recent = (System.Windows.Controls.ComboBox)settings.FindName("RecentModelsBox");
        if(settingsVm.SelectedRecentModel is not null && recent.SelectedItem is null)
            throw new Exception("The recent-model selector lost the current model.");
        CaptureWindow(settings, Path.Combine(outputDirectory, "ai-settings-light.png"));
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CryptoBook;component/Themes/DarkTheme.xaml", UriKind.Relative) });
        CaptureWindow(settings, Path.Combine(outputDirectory, "ai-settings-dark.png"));
        var scroll = (System.Windows.Controls.ScrollViewer)settings.FindName("SettingsScroll");
        scroll.ScrollToBottom();
        CaptureWindow(settings, Path.Combine(outputDirectory, "ai-settings-advanced-dark.png"));
        settingsVm.MemoryMode = AiMemoryMode.Manual;
        scroll.ScrollToBottom();
        CaptureWindow(settings, Path.Combine(outputDirectory, "ai-settings-manual-dark.png"));
        settings.Width = settings.MinWidth; settings.Height = settings.MinHeight;
        scroll.ScrollToBottom();
        CaptureWindow(settings, Path.Combine(outputDirectory, "ai-settings-small-dark.png"));
        Render((FrameworkElement)main.Content, 1200, 800, Path.Combine(outputDirectory, "ai-panel-dark.png"));
        settingsVm.Dispose();
        settings.Close();
        session.MarkSaved("AI preview.txt", new CryptoBook.FileTemplates.PlainTextTemplate());
        main.Close();
        app.Shutdown();
        Console.WriteLine("WPF UI preview passed: " + outputDirectory);
    }

    private static void VerifyPanelResizing(MainWindow main, AiAssistantViewModel ai, string outputDirectory)
    {
        var panel = (CryptoBook.MyControls.AiAssistantPanel)main.FindName("AiPanel");
        var handle = (System.Windows.Controls.Primitives.Thumb)panel.FindName("ResizeHandle");
        var root = (FrameworkElement)main.Content;
        double initialWidth = panel.ActualWidth;
        handle.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(-170, 0)
            { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
        Render(root, 1200, 800, Path.Combine(outputDirectory, "ai-panel-wide-light.png"));
        double wideWidth = panel.ActualWidth;
        if(Math.Abs(wideWidth - initialWidth - 170) > 1) throw new Exception("AI panel drag did not change its width.");
        ai.IsOpen = false;
        Render(root, 1200, 800, Path.Combine(outputDirectory, "ai-panel-closed-light.png"));
        var host = (System.Windows.Controls.Grid)VisualTreeHelper.GetParent(panel);
        if(host.ColumnDefinitions[2].ActualWidth > 1) throw new Exception("Closing AI left an empty column.");
        ai.IsOpen = true;
        Render(root, 1200, 800, Path.Combine(outputDirectory, "ai-panel-reopened-light.png"));
        if(Math.Abs(panel.ActualWidth - wideWidth) > 1) throw new Exception("AI panel lost the chosen width after reopening.");
        Render(root, 600, 800, Path.Combine(outputDirectory, "ai-panel-small-light.png"));
        if(panel.ActualWidth >= wideWidth || host.ColumnDefinitions[1].ActualWidth < 219)
            throw new Exception("AI panel did not leave space for the editor in a smaller window.");
        Render(root, 1200, 800, Path.Combine(outputDirectory, "ai-panel-restored-light.png"));
        if(Math.Abs(panel.ActualWidth - wideWidth) > 1) throw new Exception("AI panel lost the chosen width after resizing the window.");
        handle.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(10000, 0)
            { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
        Render(root, 1200, 800, Path.Combine(outputDirectory, "ai-panel-narrow-light.png"));
        if(Math.Abs(panel.ActualWidth - 280) > 1) throw new Exception("AI panel minimum width was not respected.");
        var close = (FrameworkElement)panel.FindName("ClosePanelButton");
        var closeBounds = close.TransformToAncestor(panel).TransformBounds(new Rect(close.RenderSize));
        if(closeBounds.Right > panel.ActualWidth || closeBounds.Left < 0)
            throw new Exception("The AI panel close button is clipped at minimum width.");
        handle.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(280 - initialWidth, 0)
            { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
        Render(root, 1200, 800, Path.Combine(outputDirectory, "ai-panel-light.png"));
        Console.WriteLine($"AI panel resize passed: {initialWidth:0} -> {wideWidth:0}; close/reopen, window resize and minimum width.");
    }

    private static void CaptureWindow(Window window, string path)
    {
        window.UpdateLayout();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
            (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(frame); timer.Stop();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private static void Render(FrameworkElement element, int width, int height, string path)
    {
        element.Width = width; element.Height = height;
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
            (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(frame); timer.Stop();
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
