using CryptoBook.AI;
using LLama.Native;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

internal static class ModelProbe
{
    public static async Task RunAsync(string path, string reportPath, AiMemoryMode memoryMode = AiMemoryMode.Automatic)
    {
        // Keep native diagnostics in this synthetic probe so an abort still leaves its cause.
        NativeLibraryConfig.All.WithLogCallback((_, message) => Console.Error.Write(message));
        var watch = Stopwatch.StartNew();
        using var runtime = new LocalAiInferenceService();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var answer = new StringBuilder();
        var report = new Dictionary<string, object?> { ["model"] = path, ["memory_mode"] = memoryMode.ToString(), ["context_length"] = 4096 };
        double? firstText = null;
        try
        {
            await runtime.GenerateAsync(new() { ModelPath = path, ContextLength = 4096, MaxTokens = 64,
                Temperature = 0, MemoryMode = memoryMode }, [new("user", "Reply with one word: ready")],
                text => { firstText ??= watch.Elapsed.TotalSeconds; answer.Append(text); }, null, timeout.Token);
            report["passed"] = answer.ToString().Contains("ready", StringComparison.OrdinalIgnoreCase);
            report["answer"] = answer.ToString();
        }
        catch(Exception e)
        {
            report["passed"] = false;
            report["error"] = e.ToString();
        }
        report["elapsed_seconds"] = watch.Elapsed.TotalSeconds;
        report["first_text_seconds"] = firstText;
        report["memory_plan"] = runtime.LastMemoryPlan;
        report["process_working_set_bytes"] = Environment.WorkingSet;
        await runtime.UnloadAsync();
        report["unloaded"] = !runtime.IsLoaded;
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
        if(report["passed"] is not true) Environment.ExitCode = 1;
    }
}
