using Autofac;
using CryptoBook.AI;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class AttachmentSmoke
{
    public static void Run(string model, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var app = UiPreview.CreateApplication();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var report = new Dictionary<string, object?> { ["model"] = model };
        var task = RunAsync(app, model, outputDirectory, report);
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background,
            (_, _) => { if(task.IsCompleted) frame.Continue = false; }, Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(frame); timer.Stop();
        try { task.GetAwaiter().GetResult(); report["passed"] = true; }
        catch(Exception error) { report["passed"] = false; report["error"] = error.ToString(); Environment.ExitCode = 1; }
        finally
        {
            app.Shutdown();
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"), json);
            Console.WriteLine(json);
        }
    }

    private static async Task RunAsync(Application app, string model, string directory, Dictionary<string, object?> report)
    {
        using var container = new Startup().ConfigureServices(app);
        var runtime = container.Resolve<IAiInferenceService>();
        var files = container.Resolve<AiAttachmentService>();
        var store = new AiSettingsStore(Path.Combine(directory, "settings.json"));
        var settings = new AiModelSettings { ModelPath = model, ContextLength = 4096, MaxTokens = 256, Temperature = 0 };
        store.Save(new() { Enabled = true, SelectedModelPath = model, Models = [settings] });
        using var assistant = new AiAssistantViewModel(store, runtime, container.Resolve<AiEditorContext>(),
            new AiSettingsWindowService(store, runtime), container.Resolve<IKeyResetService>(), Dispatcher.CurrentDispatcher, files);
        string document = Path.Combine(directory, "sample.docx");
        using(var output = File.Create(document))
        using(var archive = new ZipArchive(output, ZipArchiveMode.Create))
        using(var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open()))
            writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Project codename: ORBIT-731. Budget: 128450 rubles.</w:t></w:r></w:p></w:body></w:document>");
        string richText = Path.Combine(directory, "sample.rtf");
        await File.WriteAllTextAsync(richText, @"{\rtf1\ansi Budget 128450.\par Code ORBIT-731.}");
        report["rtf_extraction"] = (await files.ReadAsync(richText)).Text.Contains("ORBIT-731");
        await assistant.AttachPathsAsync([document]);
        Check(assistant.Attachments.Count == 1, assistant.Status);
        assistant.Prompt = "What is the exact project codename from the attached document? Reply with the code only.";
        await assistant.Send.ExecuteAsync();
        string answer = assistant.Messages.Last().Content;
        report["document_answer"] = answer;
        Check(answer.Contains("ORBIT-731"), "Document was not read: " + assistant.Status + "; " + answer);
        Check(assistant.Messages.First().Attachments.Count == 1 && assistant.Attachments.Count == 0, "Attachment was not retained in the sent message.");
        assistant.ClearChat.Execute(null);
        string image = Path.Combine(directory, "sample.png");
        CreateImage(image);
        await assistant.AttachPathsAsync([image]);
        Check(assistant.Attachments.Count == 1 && assistant.Attachments[0].Preview is not null, assistant.Status);
        assistant.Prompt = "Describe the shapes and their colors in this image. Answer in English, one short sentence.";
        await assistant.Send.ExecuteAsync();
        answer = assistant.Messages.Last().Content;
        report["image_answer"] = answer;
        Check(answer.Contains("red", StringComparison.OrdinalIgnoreCase) && answer.Contains("blue", StringComparison.OrdinalIgnoreCase),
            "Image colors were not recognized: " + assistant.Status + "; " + answer);
        assistant.Prompt = "What color is the circle? Answer with one English word.";
        await assistant.Send.ExecuteAsync();
        answer = assistant.Messages.Last().Content;
        report["image_followup_answer"] = answer;
        Check(answer.Contains("blue", StringComparison.OrdinalIgnoreCase), "Image history was lost: " + assistant.Status + "; " + answer);

        Task? unloading = null;
        bool stopped = false;
        var imageFile = await files.ReadAsync(image);
        try
        {
            await runtime.GenerateAsync(settings, [new("user", "Describe the image in great detail.", [imageFile])],
                _ => unloading ??= runtime.UnloadAsync(), null, CancellationToken.None);
        }
        catch(OperationCanceledException) { stopped = true; }
        if(unloading is not null) await unloading;
        report["unload_stops_image_generation"] = stopped;
        report["unloaded"] = !runtime.IsLoaded;
        Check(stopped && !runtime.IsLoaded, "Vision resources were not unloaded.");
    }

    internal static void CreateImage(string path)
    {
        var visual = new DrawingVisual();
        using(var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 512, 320));
            drawing.DrawRectangle(Brushes.Red, null, new Rect(50, 70, 170, 170));
            drawing.DrawEllipse(Brushes.Blue, null, new Point(365, 155), 85, 85);
        }
        var bitmap = new RenderTargetBitmap(512, 320, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path); png.Save(output);
    }

    private static void Check(bool condition, string error)
    {
        if(!condition) throw new Exception(error);
    }
}
