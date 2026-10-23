using CryptoBook.AI;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

internal static class PerformanceProbe
{
    internal static async Task RunAsync(string settingsPath, string outputDirectory, bool benchmark)
    {
        Directory.CreateDirectory(outputDirectory);
        var selected = new AiSettingsStore(settingsPath).Current.SelectedModel;
        // Reuse only model/runtime parameters. Never transmit or report a user's system instruction.
        var settings = selected with { SystemPrompt = "", MaxTokens = 64, Temperature = 0, UseReasoning = false };
        var report = new Dictionary<string, object?>
        {
            ["model"] = settings.ModelPath, ["context"] = settings.ContextLength,
            ["permission"] = settings.AllowPerformanceBoost, ["benchmark"] = benchmark,
            ["resources_before"] = AiResourceSnapshot.Read()
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        using var runtime = new AdaptiveAiInferenceService(new(Path.Combine(outputDirectory, "profiles.json")));
        int delivered = 0;
        runtime.RuntimeChanged += (_, _) =>
        {
            var notices = runtime.Notices;
            if(delivered > notices.Count) delivered = 0;
            foreach(var notice in notices.Skip(delivered))
            {
                Console.WriteLine(notice.ResourceKey + " " + string.Join(", ", notice.Arguments));
                if(notice.ResourceKey == "AI.PerformanceTrial")
                    Console.WriteLine("Available resources: " + JsonSerializer.Serialize(AiResourceSnapshot.Read()));
            }
            delivered = notices.Count;
        };
        var watch = Stopwatch.StartNew();
        try
        {
            if(benchmark) report["performance"] = await runtime.TestPerformanceAsync(settings, timeout.Token);
            var answer = new StringBuilder();
            await runtime.GenerateAsync(settings, [new("user", "Reply with one word: ready")],
                text => answer.Append(text), null, timeout.Token);
            if(answer.Length == 0) throw new Exception("No text generated.");
            report["answer"] = answer.ToString();
            report["runtime_options"] = runtime.RuntimeOptions;
            report["memory_plan"] = runtime.MemoryPlan;
            report["passed"] = true;
        }
        catch(Exception error)
        {
            report["passed"] = false;
            report["error"] = error is AiException ai ? ai.ResourceKey : error.GetType().Name + ": " + error.Message;
            Environment.ExitCode = 1;
        }
        finally
        {
            await runtime.UnloadAsync();
            report["elapsed_seconds"] = watch.Elapsed.TotalSeconds;
            report["resources_after"] = AiResourceSnapshot.Read();
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine(JsonSerializer.Serialize(report));
    }
}
