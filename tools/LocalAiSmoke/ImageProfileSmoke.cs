using Autofac;
using CryptoBook.AI;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>Read-only reproduction with the selected saved profile, through the panel's commands.</summary>
internal static class ImageProfileSmoke
{
    public static void Run(string settingsPath, string imagePath, string outputDirectory, int? maxEdge = null)
    {
        Directory.CreateDirectory(outputDirectory);
        var app = UiPreview.CreateApplication();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var report = new Dictionary<string, object?>();
        var task = RunAsync();
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
            Console.WriteLine($"Image profile passed: {report["passed"]}; report: {Path.Combine(outputDirectory, "result.json")}");
        }

        async Task RunAsync()
        {
            using var container = new Startup().ConfigureServices(app);
            var runtime = container.Resolve<IAiInferenceService>();
            var store = new AiSettingsStore(settingsPath); // No Save: do not alter the user's profile.
            var settings = store.Current.SelectedModel;
            report["model"] = settings.ModelPath;
            report["context"] = settings.ContextLength;
            report["max_tokens"] = settings.MaxTokens;
            report["reasoning"] = settings.UseReasoning;
            report["system_prompt_characters"] = settings.SystemPrompt.Length;
            if(imagePath == "-")
            {
                imagePath = Path.Combine(outputDirectory, "sample.png");
                AttachmentSmoke.CreateImage(imagePath);
            }
            report["image"] = imagePath;
            using var assistant = new AiAssistantViewModel(store, runtime, container.Resolve<AiEditorContext>(),
                new AiSettingsWindowService(store, runtime), container.Resolve<IKeyResetService>(), Dispatcher.CurrentDispatcher,
                container.Resolve<AiAttachmentService>());
            await assistant.AttachPathsAsync([imagePath]);
            if(assistant.Attachments.Count != 1) throw new Exception(assistant.Status);
            var image = assistant.Attachments[0];
            if(maxEdge is int edge && image.Preview is { } preview)
            {
                double scale = Math.Min(1, (double)edge / Math.Max(preview.PixelWidth, preview.PixelHeight));
                var resized = new TransformedBitmap(preview, new ScaleTransform(scale, scale)); resized.Freeze();
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(resized));
                using var bytes = new MemoryStream(); encoder.Save(bytes);
                image = image with { ImageData = bytes.ToArray(), Preview = resized };
                assistant.Attachments[0] = image;
            }
            report["normalized_width"] = image.Preview?.PixelWidth;
            report["normalized_height"] = image.Preview?.PixelHeight;
            assistant.Prompt = "Опиши, что изображено на картинке.";
            var watch = Stopwatch.StartNew();
            var states = new List<object>();
            assistant.PropertyChanged += (_, change) =>
            {
                if(change.PropertyName == nameof(assistant.Status))
                    states.Add(new { seconds = watch.Elapsed.TotalSeconds, status = assistant.Status });
            };
            using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var registration = stop.Token.Register(assistant.Send.Cancel);
            await assistant.Send.ExecuteAsync();
            report["seconds"] = watch.Elapsed.TotalSeconds;
            report["status"] = assistant.Status;
            report["states"] = states;
            string answer = assistant.Messages.LastOrDefault(message => message.Role == "assistant")?.Content ?? "";
            report["answer"] = answer;
            await runtime.UnloadAsync();
            report["unloaded"] = !runtime.IsLoaded;
            if(string.IsNullOrWhiteSpace(answer)) throw new Exception("No image answer: " + assistant.Status);
        }
    }
}
