using Autofac;
using CryptoBook.AI;
using CryptoBook.DTO;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static partial class Program
{
    private static readonly string[] Stage9Topics = ["ai.start", "ai.models", "ai.lifecycle", "ai.troubleshoot"];

    private static async Task ValidateStage9(IsolatedServices isolated, bool verify)
    {
        var c = isolated.Container;
        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        main.Width = 1440; main.Height = 960; main.WindowState = WindowState.Normal;
        if (verify)
        {
            c.Resolve<IHelpWindowService>().Open(); await Settle(main);
            var help = Application.Current.Windows.OfType<HelpWindow>().Single();
            var vm = (HelpWindowViewModel)help.DataContext;
            var content = c.Resolve<HelpContentService>();
            foreach (string id in Stage9Topics)
            {
                Check(vm.OpenTopic(id), $"Embedded stage9.1 topic opens: {id}."); await Settle(help);
                var article = content.Find(culture, id)!;
                Check(article.Images.Any() && article.Images.All(path => content.LoadImage(culture, article, path) is not null), $"Embedded stage9.1 images load: {id}.");
            }
            help.Close(); return;
        }
        string modelPath = Environment.GetEnvironmentVariable("CRYPTOBOOK_HELP_MODEL")
            ?? throw new InvalidOperationException("Set CRYPTOBOOK_HELP_MODEL to an existing language GGUF with one compatible mmproj beside it.");
        new AiModelSettings { ModelPath = modelPath }.Validate();
        string projector = AiVisionProjector.Find(modelPath);
        var store = c.Resolve<AiSettingsStore>();
        var engine = c.Resolve<IAiInferenceService>();
        var ai = c.Resolve<AiAssistantViewModel>();
        var source = (IAiRuntimeStatusSource)engine;
        string file = Path.Combine(isolated.DemoDirectory, "Demo.txt");
        File.WriteAllText(file, Local("Вымышленный план. Поездка в 09:00.", "Вигаданий план. Подорож о 09:00.", "Fiktiver Plan. Abfahrt um 09:00 Uhr.", "Fictional plan. Departure at 09:00."));
        demonstrationFile = file;
        Check((await c.Resolve<IWorkspaceFileOpenService>().OpenAsync(file)).Success, "Disposable ordinary document opens without personal data.");
        ai.Toggle.Execute(null); await Settle(main);
        var panel = Named<CryptoBook.MyControls.AiAssistantPanel>(main, "AiPanel");
        Rect PanelCrop() { var bounds = Bounds(main, panel); return new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height); }
        void PanelShot(string id, string topic, string state, Mark[]? marks = null) => Shot(main, id, topic, state, marks ?? [], PanelCrop());
        Check(ai.IsOpen && !ai.Enabled && !ai.Send.CanExecute(null) && ai.SendUnavailableReason == LocalizationManager.GetString("AI.DisabledHint"), "Disabled assistant explains unavailable Send.");
        Shot(main, "S38-a", "ai.start", "panel areas 1-3 and editor entry", [new(1, Bounds(main, Named<FrameworkElement>(main, "AiAssistantButton"))), new(2, Bounds(main, Named<Button>(main, "ModelSettingsButton"))), new(3, Bounds(main, Named<Thumb>(main, "ResizeHandle")))]);

        void Click(Window w, string name) => ((Button)w.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        AiModelSettingsWindow OpenSettings() { ai.OpenSettings.Execute(null); return Application.Current.Windows.OfType<AiModelSettingsWindow>().Single(); }
        var settings = OpenSettings(); await Settle(settings);
        var draft = (AiModelSettingsViewModel)settings.DataContext;
        draft.Enabled = true; draft.ModelPath = modelPath; settings.Close();
        Check(!store.Current.Enabled && store.Current.SelectedModelPath.Length == 0, "Closing model settings discards unsaved enabling and path changes.");
        settings = OpenSettings(); await Settle(settings); draft = (AiModelSettingsViewModel)settings.DataContext;
        draft.Enabled = true;
        var pathBox = (TextBox)settings.FindName("ModelPathBox");
        pathBox.Text = modelPath;
        pathBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        ((TextBox)settings.FindName("TokensBox")).Text = "128";
        draft.Templates.SelectedTemplate!.SystemPrompt = Local("Отвечай по-русски. Кратко и точно.", "Відповідай українською. Стисло й точно.", "Antworte kurz und genau auf Deutsch.", "Reply briefly and accurately in English.");
        await Settle(settings);
        Shot(settings, "S38-b", "ai.models", "steps 2-4: enable, language GGUF and Save", []);
        Click(settings, "SaveModelButton"); await Settle(main);
        Check(!settings.IsVisible && store.Current.Enabled && store.Current.SelectedModelPath == modelPath && store.Current.SelectedModel.MaxTokens == 128,
            "Real settings Save commits pending path and reply-limit bindings to the isolated store.");
        Check(!engine.IsLoaded, "Saving a model without preload does not load weights.");

        settings = OpenSettings(); await Settle(settings); draft = (AiModelSettingsViewModel)settings.DataContext;
        Click(settings, "TestModelButton");
        await AiUntil(() => !draft.IsBusy, "Model check", 360);
        Check(draft.Status == LocalizationManager.GetString("AI.ModelWorks") && engine.IsLoaded, "Real Check model returns a nonempty answer and retains loaded weights.");
        await Settle(settings); Shot(settings, "S38-c", "ai.models", "step 3: actual successful model check and runtime", []); settings.Close();
        await ai.Unload.ExecuteAsync();

        ai.Prompt = Local("Ответь одним словом: готово.", "Відповідай одним словом: готово.", "Antworte mit einem Wort: bereit.", "Reply with one word: ready.");
        Task send = ai.Send.ExecuteAsync();
        await AiUntil(() => engine.IsBusy && !engine.IsLoaded, "Actual load begins");
        await Settle(main);
        Check(ai.Runtime.ModelStatus == LocalizationManager.GetString("AI.ModelStatus.Loading"), "Actual first request shows Loading before weights are ready.");
        PanelShot("S39-a", "ai.lifecycle", "actual loading, not a staged label");
        await send;
        Check(engine.IsLoaded && !ai.IsBusy && ai.Messages.Last().HasContent, "Normal response completion keeps loaded weights and a nonempty real reply.");
        string completed = ai.Messages.Last().Content;
        await Settle(main); PanelShot("S39-b", "ai.lifecycle", "actual completed answer; model remains loaded");
        ai.Toggle.Execute(null); await Settle(main); Check(!ai.IsOpen && engine.IsLoaded, "Closing the idle panel retains the loaded model.");
        ai.Toggle.Execute(null); await Settle(main);
        var handle = Named<Thumb>(main, "ResizeHandle");
        double previousWidth = panel.ActualWidth;
        handle.RaiseEvent(new DragDeltaEventArgs(-100, 0) { RoutedEvent = Thumb.DragDeltaEvent }); await Settle(main);
        Check(panel.ActualWidth > previousWidth, "Routed production resize handle increases panel width.");
        ai.Toggle.Execute(null); ai.Toggle.Execute(null); await Settle(main);
        Check(Math.Abs(panel.ActualWidth - previousWidth - 100) < 2, "Resized width survives panel close/reopen within the isolated profile.");

        ai.Prompt = Local("Перечисли 100 разных идей для вымышленного путешествия. Пронумеруй каждую и подробно опиши.", "Перелічи 100 різних ідей для вигаданої подорожі. Пронумеруй і докладно опиши кожну.", "Nenne 100 verschiedene Ideen für eine fiktive Reise. Nummeriere und beschreibe jede ausführlich.", "List 100 different ideas for a fictional trip. Number each and describe each in detail.");
        send = ai.Send.ExecuteAsync();
        await AiUntil(() => ai.IsBusy && ai.Messages.Last().Content.Length > 50, "Streaming reply");
        Check(ai.Stop.CanExecute(null), "Stop is enabled while actual output streams.");
        PanelShot("S39-c", "ai.lifecycle", "actual streaming before Stop", [new(1, Bounds(main, Named<Button>(main, "StopButton")))]);
        ai.Stop.Execute(null); await send; await Settle(main);
        Check(engine.IsLoaded && !ai.IsBusy && ai.Messages.Last().HasContent && ai.Status == LocalizationManager.GetString("AI.Stopped"), "Cooperative Stop retains partial output and loaded weights.");
        string partial = ai.Messages.Last().Content;
        PanelShot("S39-d", "ai.lifecycle", "actual stopped partial response, weights retained");
        await ai.Unload.ExecuteAsync(); await Settle(main);
        Check(!engine.IsLoaded && ai.Messages.Last().Content == partial, "Explicit unload releases weights without deleting the partial transcript.");
        PanelShot("S39-e", "ai.lifecycle", "explicitly unloaded, transcript preserved", [new(2, Bounds(main, Named<Button>(main, "UnloadModelButton")))]);
        await ai.ClearChat.ExecuteAsync();

        // A deterministic synthetic image, generated as a fixture rather than a screenshot.
        string imagePath = Path.Combine(isolated.DemoDirectory, "Shapes.png");
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        { dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 256, 256)); dc.DrawRectangle(Brushes.Red, null, new Rect(20, 20, 100, 100)); dc.DrawEllipse(Brushes.Blue, null, new Point(180, 180), 48, 48); }
        var image = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32); image.Render(drawing); WriteBitmap(image, imagePath);
        await ai.AttachPaths.ExecuteAsync(new[] { imagePath });
        Check(ai.Attachments.Count == 1 && ai.Attachments[0].IsImage, "Production attachment reader accepts the synthetic shape fixture.");
        ai.Prompt = Local("Назови цвета двух фигур на картинке одним коротким предложением.", "Назви кольори двох фігур на зображенні одним коротким реченням.", "Nenne die Farben der beiden Formen im Bild in einem kurzen Satz.", "Name the colors of the two shapes in the image in one short sentence.");
        await ai.Send.ExecuteAsync(); await Settle(main);
        Check(engine.IsLoaded && ai.Messages.Last().HasContent, "Real image request with the local compatible projector produces output.");
        string vision = ai.Messages.Last().Content;
        Check(new[] { "red", "красн", "червон", "rot" }.Any(s => vision.Contains(s, StringComparison.OrdinalIgnoreCase)) &&
            new[] { "blue", "син", "blau" }.Any(s => vision.Contains(s, StringComparison.OrdinalIgnoreCase)), "Real vision reply identifies both fixture colors.");
        PanelShot("S38-d", "ai.models", "real vision reply for the two-color synthetic image");

        // Resolve only this service's owned process. Never discover or terminate other application instances.
        var owned = typeof(AdaptiveAiInferenceService).GetField("worker", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        int pid = (int)owned.GetType().GetProperty("ProcessId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owned)!;
        using (var process = Process.GetProcessById(pid))
        {
            Check(process.ProcessName == "CryptoBook", "Unexpected-exit fixture targets this isolated AI service's owned CryptoBook worker only.");
            process.Kill(); await process.WaitForExitAsync();
        }
        await AiUntil(() => ai.Runtime.ModelStatus == LocalizationManager.GetString("AI.ModelStatus.Unloaded"), "Idle worker-exit detection");
        Check(!engine.IsLoaded && ai.Messages.Last().Content == vision, "Idle worker exit is detected without losing the received answer.");
        await ai.ClearChat.ExecuteAsync(); ai.Prompt = Local("Ответь: готово.", "Відповідай: готово.", "Antworte: bereit.", "Reply: ready.");
        await ai.Send.ExecuteAsync();
        Check(engine.IsLoaded && ai.Messages.Last().HasContent, "A fresh request recovers after the owned worker unexpectedly exits.");
        int messages = ai.Messages.Count;
        await (Task)typeof(AiAssistantViewModel).GetMethod("CheckIdleAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ai, [engine.LastUsedUtc.AddMinutes(11)])!;
        Check(!engine.IsLoaded && ai.Messages.Count == messages, "Production idle check unloads after an advanced 11-minute clock without deleting chat.");

        settings = OpenSettings(); await Settle(settings); draft = (AiModelSettingsViewModel)settings.DataContext;
        ((TextBox)settings.FindName("ModelPathBox")).Text = Path.Combine(isolated.DemoDirectory, "Missing.gguf");
        Click(settings, "SaveModelButton"); await Settle(settings);
        Check(settings.IsVisible && draft.Status == LocalizationManager.GetString("AI.ModelMissing") && store.Current.SelectedModelPath == modelPath, "Missing GGUF blocks Save, leaves prior configuration intact and explains the error.");
        Shot(settings, "S39-f", "ai.troubleshoot", "real missing-file validation before saving", []); settings.Close();
        await ai.Unload.ExecuteAsync(); ai.Enabled = false; await Settle(main);
        Check(!ai.Send.CanExecute(null) && !engine.IsLoaded, "Disabling the assistant blocks sending and releases its model.");
        WriteJson(Path.GetRelativePath(repo, Path.Combine(isolated.Root, "stage9.json")), new
        {
            culture, model = Path.GetFileName(modelPath), projector = Path.GetFileName(projector),
            modelBytes = new FileInfo(modelPath).Length, projectorBytes = new FileInfo(projector).Length,
            completed, partial, vision, runtime = source.RuntimeOptions,
            realGeneration = true, cooperativeStopRetainedWeights = true, explicitUnload = true, idleClockAdvancedMinutes = 11,
            unexpectedExit = "Only this isolated service's owned idle worker terminated, detected and recovered", nativePicker = "not exercised",
            fixture = "Generated red square and blue circle on white, 256x256; no personal content"
        });
    }

    private static async Task AiUntil(Func<bool> condition, string operation, int seconds = 90)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed.TotalSeconds > seconds) throw new TimeoutException(operation);
            await Task.Delay(25);
        }
    }
}
