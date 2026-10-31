using Autofac;
using CryptoBook;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Properties = CryptoBook.Properties;

internal static partial class Program
{
    private static string repo = "", culture = "", commit = "";
    private static string? demonstrationFile;
    private static string? captureFilter;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static int checks;
    private static readonly List<string> bindingErrors = [];
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine("HelpValidation <repository> <culture> <commit> [verify]"); return 2; }
        repo = Path.GetFullPath(args[0]); culture = args[1]; commit = args[2];
        bool verify = args.Length > 3 && args[3] == "verify";
        bool stage4 = args.Length > 3 && args[3].StartsWith("stage4", StringComparison.Ordinal);
        bool stage5 = args.Length > 3 && args[3].StartsWith("stage5", StringComparison.Ordinal);
        bool stage6 = args.Length > 3 && args[3].StartsWith("stage6", StringComparison.Ordinal);
        bool stage7 = args.Length > 3 && args[3].StartsWith("stage7", StringComparison.Ordinal);
        captureFilter = args.Length > 4 ? args[4] : null;
        if (!new[] { "en-US", "ru-RU", "uk-UA", "de-DE" }.Contains(culture)) return 2;
        string runRoot = Path.Combine(repo, "artifacts", "hv", culture, Guid.NewGuid().ToString("N")[..8]);
        var settings = MemorySettingsProvider.Install();
        Properties.Settings.Default.CultureInfo = culture;
        Properties.Settings.Default.FileOverviewFirstStart = true;
        Properties.Settings.Default.WindowState = "Normal";
        var isolated = new IsolatedServices(runRoot, stage4 || stage5 || stage6 || stage7, stage7);
        var app = new App(isolated.Configure) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingListener(bindingErrors));
        int result = 1;
        app.DispatcherUnhandledException += (_, e) => { Console.Error.WriteLine(e.Exception); e.Handled = true; Cleanup(isolated); app.Shutdown(1); };
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                if (stage7) await ValidateStage7(isolated, args[3] == "stage7-verify");
                else if (stage6) await ValidateStage6(isolated, args[3] == "stage6-verify");
                else if (stage5) await ValidateStage5(isolated, args[3] == "stage5-verify");
                else if (stage4) await ValidateStage4(isolated, args[3] == "stage4-verify");
                else await Validate(isolated, verify);
                Check(settings.SaveCount > 0, "In-memory settings Save was exercised.");
                Check(bindingErrors.Count == 0, "No WPF binding errors.");
                File.WriteAllText(Path.Combine(runRoot, "result.json"), JsonSerializer.Serialize(new
                {
                    culture, commit, dirtyWorkingTree = true, checks, checkMessages, stage4, stage5, stage6, stage7, mode = args.Length > 3 ? args[3] : "capture", bindingErrors, settingsSaveCount = settings.SaveCount,
                    storageRoot = isolated.Root, stores = isolated.Paths, appStartup = "App.OnStartup with explicit isolated services",
                    captureMethod = "RenderTargetBitmap of visible production WPF windows",
                    interaction = stage7 ? "production local name/content/document searches, routed search keys, previews, image player and actual silent AVI playback; print command preparation recorded by an isolated sink"
                        : stage6 ? "production file-manager commands, local disk pipelines, WPF folder/name/conflict/batch/details dialogs; isolated stores and disposable clipboard with prior data restored in memory"
                        : stage5 ? "visible production control DataContexts and commands; real link/crop dialogs; production image/layout/crop services; image paths and unsaved decisions supplied by queues; disposable clipboard roundtrip with prior data object restored in memory"
                        : stage4 ? "production WPF ICommand/bindings and disk pipelines; native Save As and unsaved choices supplied by queues" : "production ICommand/ViewModel and storage services, plus routed keyboard events",
                    limitations = stage7 ? "No Android/MTP write or transfer, physical keyboard/mouse, external PDF application, native print-dialog interaction or physical print. D3D video pixels are not captured by RenderTargetBitmap; video playback is checked through real player state/position. Print sink verifies preparation only."
                        : stage6 ? "Local disposable files only. No physical gestures, shell Open With/Explorer/Windows properties launch, Android/MTP or installed-user migration. DropCommand is exercised; external file/folder pickers use the isolated demo destination. Cancellation at progress startup is live; mid-operation rollback/fault injection is covered by targeted tests."
                        : stage5 ? "No physical keyboard/mouse, drag gesture, installed-user migration or Windows scaling test. Image resize/layout/crop services and routed image selection are exercised; the native image picker is replaced by a disposable path queue. See image-undo.json and bookmark-roundtrip.json for observed product limitations."
                        : "No physical keyboard/mouse or installed-user migration test. Folder-picker shortcut supplies the demo folder; stage4 also exercises the real WPF folder picker."
                }, Json));
                result = 0;
                Console.WriteLine($"HELP_VALIDATION PASS culture={culture} checks={checks} mode={(args.Length > 3 ? args[3] : "capture")} report={Path.Combine(runRoot, "result.json")}");
            }
            catch (Exception e) { Console.Error.WriteLine(e); }
            finally { Cleanup(isolated); app.Shutdown(result); }
        }, DispatcherPriority.ApplicationIdle);
        Directory.SetCurrentDirectory(runRoot);
        app.Run();
        if (app.StartupFailure is not null) Console.Error.WriteLine(app.StartupFailure);
        return result;
    }

    private static void Cleanup(IsolatedServices isolated)
    {
        if (isolated.Container is null) return;
        ((IWorkspaceDocumentSession)isolated.Container.Resolve<IDocumentSession>()).RestoreInactiveDocument(null);
        isolated.Container.Resolve<IDocumentSession>().Close();
    }

    private static async Task Validate(IsolatedServices isolated, bool verify)
    {
        var c = isolated.Container;
        var main = System.Windows.Application.Current.Windows.OfType<MainWindow>().Single();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        main.WindowState = WindowState.Normal; main.Width = 1440; main.Height = 960;
        var windows = c.Resolve<IWindowManager>();
        var scope = windows.FindHostWindow(((IMainWindowViewModel)main.DataContext).WindowId)!.Scope;
        var session = c.Resolve<IDocumentSession>();
        var editor = c.Resolve<IRichTextBoxService>();
        var navigation = scope.Resolve<IPageNavigationService>();
        navigation.Navigate("Home");
        await Settle(main);
        Check(!session.HasDocument, "Clean isolated start screen.");
        if (!verify) Shot(main, "S02-a", "start.interface", "areas 1-2", [new(1, new Rect(1, 1, 1438, 25), new Point(1250, 50)),
            new(2, new Rect(28, 138, 1000, 56), new Point(1050, 162))]);

        scope.Resolve<IHomeViewModel>().NewDocument.Execute(null);
        await Settle(main);
        var dialog = System.Windows.Application.Current.Windows.OfType<NewFileDialog>().Single();
        dialog.Width = 840; dialog.Height = 900;
        var dialogHost = windows.FindHostWindow(((INewFileDialogViewModel)dialog.DataContext).WindowId)!;
        var vm = (INewFileDialogViewModel)dialog.DataContext;
        vm.Browse.Execute(null);
        for (int attempt = 0; attempt < 100 && (vm.IsBusy || vm.TargetDirectory != isolated.DemoDirectory); attempt++)
            await Task.Delay(20);
        Check(vm.TargetDirectory == isolated.DemoDirectory, "Browse uses the isolated demo folder.");
        vm.SelectedTemplate = vm.Templates.Single(t => t is RichTextFileTemplate);
        vm.SelectedNewTemplate.Execute(null);
        string name = culture switch { "ru-RU" => "План поездки.rtf", "uk-UA" => "План подорожі.rtf", "de-DE" => "Reiseplan.rtf", _ => "Trip plan.rtf" };
        string line = culture switch { "ru-RU" => "Выезд в 09:00.", "uk-UA" => "Виїзд о 09:00.", "de-DE" => "Abfahrt um 09:00 Uhr.", _ => "Departure at 09:00." };
        vm.FileName = name;
        demonstrationFile = Path.Combine(vm.TargetDirectory, name);
        vm.SelectedDocumentTemplate = vm.DocumentTemplates.Single(t => t.Id == "empty");
        vm.PaperSize = DocumentPaperSize.A4; vm.IsLandscape = false; vm.IsReadOnly = false; vm.IsHidden = false;
        await Settle(dialog);
        if (!verify) Shot(dialog, "S04-a", "doc.create", "steps 2-7", [
            new(2, Bounds(dialog, Visuals<TextBlock>(dialog).First(t => t.Text == isolated.DemoDirectory))),
            new(3, Bounds(dialog, Visuals<ComboBox>(dialog).First(b => b.SelectedItem is RichTextFileTemplate))),
            new(4, Bounds(dialog, Named<FrameworkElement>(dialog, "FileNameTextBox"))),
            new(5, Bounds(dialog, Named<FrameworkElement>(dialog, "DocumentTemplateSelector"))),
            new(6, Bounds(dialog, Visuals<ComboBox>(dialog).First(b => b.SelectedItem is DocumentPaperSize))),
            new(7, Bounds(dialog, Buttons(dialog).Single(b => b.Command == vm.Create)))]);
        Check(vm.Create.CanExecute(null), "Create is available.");
        await ((AsyncRelayCommand)vm.Create).ExecuteAsync();
        await Settle(main);
        Check(!dialog.IsVisible && session.FilePath == Path.Combine(isolated.DemoDirectory, name), "Create writes and opens the requested RTF.");
        if (!verify) Shot(main, "S04-b", "doc.create", "step 7", []);
        var mainModel = scope.Resolve<IMainWindowModel>();
        mainModel.IsMenuOpen = true; await Settle(main);
        if (!verify)
        {
            Shot(main, "S02-b", "start.interface", "areas 3-4", [new(3, new Rect(6, 50, 270, 820), new Point(300, 75)), new(4, new Rect(760, 260, 580, 480))]);
            var ai = Named<FrameworkElement>(main, "AiAssistantButton");
            Rect aiBounds = Bounds(main, ai);
            Rect crop = new(430, Math.Max(0, aiBounds.Top - 35), Math.Min(1000, main.ActualWidth - 430), 110);
            Shot(main, "S02-c", "start.interface", "area 5", [new(5, aiBounds)], crop);
        }
        mainModel.IsMenuOpen = false;
        editor.Document.Blocks.Add(new Paragraph(new Run(line)));
        session.MarkDirty();
        mainModel.IsMenuOpen = true; await Settle(main);
        var menu = scope.Resolve<IMenuFileViewModel>();
        Check(menu.SaveFile.CanExecute(null), "Save is available after editing.");
        if (!verify) Shot(main, "S06-a", "doc.create", "step 9", [new(9, Bounds(main, Buttons(main).Last(b => b.Command == menu.SaveFile)))], new Rect(0, 40, 900, 850));
        await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync();
        Check(!session.IsDirty, "Save clears dirty state after writing.");
        mainModel.IsMenuOpen = false;
        string savedPath = session.FilePath!;
        await ((AsyncRelayCommand)menu.CloseFile).ExecuteAsync();
        var open = await c.Resolve<IWorkspaceFileOpenService>().OpenAsync(savedPath);
        Check(open.Success && new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.Contains(line), "Reopening the saved file retains the entered text.");
        await Settle(main);
        if (!verify) Shot(main, "S06-b", "doc.create", "result after reopening", []);

        // Preserve a dirty ordinary document, a separate Markdown document, a selection and chat text.
        string mdPath = Path.Combine(isolated.DemoDirectory, "separate.md");
        File.WriteAllText(mdPath, "# Separate Markdown\n\nOriginal text.");
        Check((await c.Resolve<IWorkspaceFileOpenService>().OpenAsync(mdPath)).Success, "Separate Markdown opens.");
        var md = c.Resolve<IMarkdownDocumentState>(); md.Text += "\nUnsaved Markdown.";
        navigation.Navigate("Home");
        await c.Resolve<IWorkspaceFileOpenService>().OpenAsync(Path.Combine(isolated.DemoDirectory, name));
        editor.Document.Blocks.Add(new Paragraph(new Run("Unsaved ordinary document."))); session.MarkDirty();
        await Settle(main);
        string ordinary = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
        var workspace = ((IWorkspaceDocumentSession)c.Resolve<IDocumentSession>());
        var inactive = workspace.CaptureInactiveDocument();
        Check(inactive is { IsMarkdown: true } && Encoding.UTF8.GetString(inactive.Content).Contains("Unsaved Markdown."), "Independent dirty Markdown is parked.");
        editor.Selection.Select(editor.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward),
            editor.Document.ContentStart.GetPositionAtOffset(10, LogicalDirection.Forward)!);
        string selection = editor.Selection.Text;
        var aiVm = c.Resolve<CryptoBook.AI.AiAssistantViewModel>(); aiVm.Prompt = "Demonstration prompt, no model call.";
        var chatMessage = new CryptoBook.AI.AiChatMessage("user", "Demonstration chat message."); aiVm.Messages.Add(chatMessage);
        string prompt = aiVm.Prompt;
        string? page = navigation.CurrentKey;
        var service = c.Resolve<IHelpWindowService>();
        var visibleEditor = Visuals<System.Windows.Controls.RichTextBox>(main).Single(r => r.IsVisible);
        visibleEditor.Focus();
        visibleEditor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(main), Environment.TickCount, Key.F1)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        await Settle(main);
        var help = System.Windows.Application.Current.Windows.OfType<HelpWindow>().Single();
        var helpVm = (HelpWindowViewModel)help.DataContext;
        Check(help.Owner == main && main.IsEnabled, "F1 from the editor opens modeless help owned by MainWindow.");
        Check(helpVm.CurrentTopicId == "start.interface", "First help topic.");
        service.Open(); Check(System.Windows.Application.Current.Windows.OfType<HelpWindow>().Count() == 1, "Repeated opening uses one instance.");
        help.WindowState = WindowState.Minimized; service.Open(); Check(help.WindowState == WindowState.Normal, "Repeated opening restores a minimized window.");
        helpVm.OpenTopic("doc.create"); helpVm.Back.Execute(null); Check(helpVm.CurrentTopicId == "start.interface", "Back works.");
        helpVm.Forward.Execute(null); Check(helpVm.CurrentTopicId == "doc.create", "Forward works.");
        Check(!helpVm.OpenTopic("security.password") && helpVm.CurrentTopicId == "doc.create", "Planned target preserves article and history.");
        helpVm.Query = "09:00"; helpVm.RunSearch(); Check(helpVm.Results.Single().Id == "doc.create", "Search includes visible article text.");
        helpVm.Query = "zzzz-no-such-help-topic"; helpVm.RunSearch(); Check(helpVm.HasNoResults, "No-result state.");
        helpVm.Query = "  "; Check(!helpVm.IsSearching, "Whitespace query returns the contents tree.");
        helpVm.Query = "Dateiname";
        LocalizationManager.SelectCulture(culture == "de-DE" ? "ru-RU" : "de-DE");
        Check(helpVm.Query == "" && helpVm.Results.Count == 0 && helpVm.CurrentTopicId == "doc.create", "Culture change clears search and preserves topic.");
        Check(helpVm.Back.CanExecute(null), "Culture change preserves history.");
        LocalizationManager.SelectCulture(culture); await Settle(help);
        var inactiveAfter = workspace.CaptureInactiveDocument();
        Check(ordinary == new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text &&
            inactiveAfter is not null && inactive!.Content.SequenceEqual(inactiveAfter.Content) && inactive.Revision == inactiveAfter.Revision &&
            selection == editor.Selection.Text && prompt == aiVm.Prompt && aiVm.Messages.Contains(chatMessage) && session.IsDirty && navigation.CurrentKey == page,
            "Help preserves both documents, dirty revisions, selection, AI messages/input and main navigation.");

        mainModel.IsMenuOpen = true; await Settle(main);
        var helpMenuButton = Buttons(main).Single(b => b.ToolTip as string == "F1");
        Check(helpMenuButton.IsEnabled && helpMenuButton.Command.CanExecute(null), "Localized Help menu entry is available.");
        helpMenuButton.Command.Execute(null); await Settle(help);
        Check(System.Windows.Application.Current.Windows.OfType<HelpWindow>().Count() == 1, "Menu reuses the existing Help window.");
        mainModel.IsMenuOpen = false;
        navigation.Navigate("MarkdownEditor"); await Settle(main);
        var markdownEditor = (IMarkdownEditorViewModel)navigation.CurrentPage!.DataContext;
        markdownEditor.OpenSyntaxHelp.Execute(null); await Settle(help);
        Check(helpVm.CurrentTopicId == "md.syntax" && navigation.CurrentKey == "MarkdownEditor", "Markdown syntax opens separate help without replacing the main page.");
        helpVm.OpenTopic("doc.create");
        navigation.Navigate("Home"); await Settle(main);
        service.Open(); await Settle(help);
        var searchBox = (TextBox)help.FindName("HelpSearch");
        searchBox.Focus(); helpVm.Query = "09:00";
        RoutedKey(help, Key.Escape); Check(helpVm.Query.Length == 0, "Escape clears the focused help search.");
        RoutedKey(help, Key.F1); Check(helpVm.CurrentTopicId == "doc.create", "F1 inside Help preserves the current topic.");
        Check(searchBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)), "Tab traversal moves focus.");
        Check(Keyboard.FocusedElement is DependencyObject focused && Window.GetWindow(focused) == help, "Tab traversal remains in Help.");
        Check(((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)), "Shift+Tab traversal moves back.");

        foreach (ApplicationTheme theme in Enum.GetValues<ApplicationTheme>())
        {
            c.Resolve<IThemeManager>().ApplyTheme(theme); await Settle(help);
            if (verify) SaveWindow(help, Path.Combine(isolated.Root, "help-" + theme + ".png"));
        }
        help.Width = 720; help.Height = 480; await Settle(help);
        Check(help.ActualWidth >= 720 && help.ActualHeight >= 480, "Narrow two-pane layout.");
        var articleScroll = Visuals<ScrollViewer>((FlowDocumentScrollViewer)help.FindName("ArticleViewer")).First();
        Check(articleScroll.ExtentWidth <= articleScroll.ViewportWidth + 1, "Article text and images reflow without scrolling the entire page horizontally.");
        if (verify)
        {
            var content = c.Resolve<HelpContentService>();
            var topic = content.Find(culture, "doc.create")!;
            var bitmap = content.LoadImage(culture, topic, topic.Images[0]);
            Check(bitmap is not null, "Embedded localized image loads without repository file reads.");
            helpVm.ShowImage.Execute(new HelpImage(bitmap!, "Image verification"));
            await Settle(help); Check(helpVm.IsImageOpen, "Image enlargement opens.");
            RoutedKey(help, Key.Escape); Check(!helpVm.IsImageOpen, "Escape closes image enlargement.");
            SaveWindow(help, Path.Combine(isolated.Root, "help-narrow.png"));
            SaveWindow(help, Path.Combine(isolated.Root, "help-144dpi.png"), 144);
        }
        help.Close();
        service.Open(); await Settle(main);
        help = System.Windows.Application.Current.Windows.OfType<HelpWindow>().Single();
        Check(((HelpWindowViewModel)help.DataContext).CurrentTopicId == "doc.create", "Last topic survives close and reopen.");
        help.Close();
        Check(isolated.Paths.All(p => p.StartsWith(isolated.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)), "All registered persistent stores are inside the validation root.");
    }

    private sealed record Mark(int Number, Rect Bounds, Point? Badge = null);
    private static void Shot(Window window, string id, string topic, string step, Mark[] marks, Rect? crop = null, FrameworkElement? surface = null)
    {
        if (captureFilter is not null && !captureFilter.Split(',').Contains(id, StringComparer.Ordinal)) return;
        string raw = $"docs/help/screenshots/raw/{culture}/{id}.png", annotation = $"docs/help/screenshots/annotations/{culture}/{id}.json";
        string output = $"docs/help/content/{culture}/images/{id}.png", passport = $"docs/help/screenshots/passports/{culture}/{id}.json";
        var bitmap = surface is null ? Capture(window, 96) : CaptureSurface(surface);
        WriteBitmap(bitmap, raw);
        Rect region = crop ?? new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
        region.Intersect(new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, region.Width, region.Height)));
            dc.DrawImage(bitmap, new Rect(-region.X, -region.Y, bitmap.PixelWidth, bitmap.PixelHeight));
            foreach (var mark in marks)
            {
                var rect = mark.Bounds; rect.Offset(-region.X, -region.Y);
                dc.DrawRectangle(null, new Pen(Brushes.Black, 5), rect);
                dc.DrawRectangle(null, new Pen(Brushes.Gold, 2), rect);
                var badge = mark.Badge is Point absolute ? new Point(absolute.X - region.X, absolute.Y - region.Y)
                    : new Point(Math.Max(14, rect.Left - 18), Math.Max(14, rect.Top + 13));
                if (mark.Badge is null && rect.Left < 30 && rect.Right + 32 < region.Width)
                    badge.X = rect.Right + 18;
                dc.DrawEllipse(Brushes.Gold, new Pen(Brushes.Black, 2), badge, 13, 13);
                var number = new FormattedText(mark.Number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, Brushes.Black, 1);
                dc.DrawText(number, new Point(badge.X - number.Width / 2, badge.Y - number.Height / 2));
            }
            dc.Pop();
        }
        var annotated = new RenderTargetBitmap((int)region.Width, (int)region.Height, 96, 96, PixelFormats.Pbgra32);
        annotated.Render(visual); WriteBitmap(annotated, output);
        WriteJson(annotation, new { shotId = id, culture, rawFile = raw, crop = crop is null ? null : RectData(region),
            marks = marks.Select(m => new { number = m.Number, shape = "rectangle", x = (m.Bounds.X-region.X)/region.Width,
                y = (m.Bounds.Y-region.Y)/region.Height, width = m.Bounds.Width/region.Width, height = m.Bounds.Height/region.Height,
                badge = m.Badge is Point point ? new { x = (point.X-region.X)/region.Width, y = (point.Y-region.Y)/region.Height } : null }) });
        WriteJson(passport, new { shotId = id, topicId = topic, articleStepOrArea = step, culture, osCulture = CultureInfo.InstalledUICulture.Name,
            version = typeof(App).Assembly.GetName().Version?.ToString(), commit, dirtyWorkingTree = true, capturedAt = DateTimeOffset.UtcNow,
            captureMethod = surface is null ? "RenderTargetBitmap; visible production WPF; isolated App.OnStartup" : "RenderTargetBitmap of visible production WPF popup child; isolated App.OnStartup", theme = "Light", windowDip = new { width = window.ActualWidth, height = window.ActualHeight },
            rawPixels = new { width = bitmap.PixelWidth, height = bitmap.PixelHeight }, outputPixels = new { width = annotated.PixelWidth, height = annotated.PixelHeight }, dpi = 96,
            rawFile = raw, annotationFile = annotation, outputFile = output, actionCheck = "Production commands exercised separately in this run; folder picker supplies the demo folder; no physical mouse/keyboard.",
            demoFile = demonstrationFile,
            visualCheck = "Pending visual review of generated PNG." });
        Console.WriteLine($"SHOT {culture}/{id} {annotated.PixelWidth}x{annotated.PixelHeight}");
    }
    private static object RectData(Rect r) => new { x = r.X, y = r.Y, width = r.Width, height = r.Height };
    private static BitmapSource CaptureSurface(FrameworkElement surface)
    {
        surface.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth), (int)Math.Ceiling(surface.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface); return bitmap;
    }
    private static void WriteJson(string path, object value)
    { string full = Path.Combine(repo, path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, JsonSerializer.Serialize(value, Json)); }
    private static void WriteBitmap(BitmapSource source, string relative)
    { string path = Path.IsPathRooted(relative) ? relative : Path.Combine(repo, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); using var stream = File.Create(path); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); encoder.Save(stream); }
    private static BitmapSource Capture(Window window, double dpi)
    {
        window.UpdateLayout(); double width = window.ActualWidth, height = window.ActualHeight;
        if (window.WindowStyle != WindowStyle.None && window.Content is FrameworkElement client)
        { width = client.ActualWidth + client.Margin.Left + client.Margin.Right; height = client.ActualHeight + client.Margin.Top + client.Margin.Bottom; }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width*dpi/96), (int)Math.Ceiling(height*dpi/96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(window); return bitmap;
    }
    private static void SaveWindow(Window window, string path, double dpi = 96) => WriteBitmap(Capture(window, dpi), path);
    private static Rect Bounds(Window window, FrameworkElement element) => element.TransformToAncestor(window).TransformBounds(new Rect(element.RenderSize));
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    { for (int i=0; i<VisualTreeHelper.GetChildrenCount(root); i++) { var child=VisualTreeHelper.GetChild(root,i); if(child is T match) yield return match; foreach(var nested in Visuals<T>(child)) yield return nested; } }
    private static T Named<T>(Window window, string name) where T : FrameworkElement => Visuals<T>(window).First(e => e.Name == name);
    private static IEnumerable<Button> Buttons(Window window) => Visuals<Button>(window).Where(b => b.IsVisible);
    private static async Task Settle(Window window) { window.UpdateLayout(); await Task.Delay(220); await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle); }
    private static readonly List<string> checkMessages = [];
    private static void Check(bool condition, string message) { checks++; if(!condition) throw new InvalidOperationException(message); checkMessages.Add(message); }
    private static void RoutedKey(Window window, Key key) => window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
        PresentationSource.FromVisual(window), Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    private sealed class BindingListener(List<string> errors) : TraceListener
    { public override void Write(string? message) { } public override void WriteLine(string? message) { if(!string.IsNullOrWhiteSpace(message)) errors.Add(message); } }
}
