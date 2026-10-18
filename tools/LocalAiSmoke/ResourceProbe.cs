using CryptoBook.AI;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

/// <summary>Observe generation, idle residency and unload using a saved profile without modifying it.</summary>
internal static class ResourceProbe
{
    public static async Task RunAsync(string settingsPath, string directory)
    {
        Directory.CreateDirectory(directory);
        var settings = new AiSettingsStore(settingsPath).Current.SelectedModel;
        using var runtime = new LocalAiInferenceService();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var sampling = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();
        var answer = new StringBuilder();
        double? firstText = null;
        string phase = "loading";
        var samples = new List<Sample>();
        var report = new Dictionary<string, object?>
        {
            ["model"] = settings.ModelPath,
            ["memory_mode"] = settings.MemoryMode.ToString(),
            ["context"] = settings.ContextLength,
            ["probe_reply_limit"] = 512,
            ["system_prompt_characters"] = settings.SystemPrompt.Length,
            ["logical_processors"] = Environment.ProcessorCount
        };
        var monitor = Task.Run(async () =>
        {
            using var process = Process.GetCurrentProcess();
            var previousCpu = process.TotalProcessorTime;
            double previousTime = watch.Elapsed.TotalSeconds;
            while(!sampling.IsCancellationRequested)
            {
                string sampledPhase = Volatile.Read(ref phase);
                string gpu = await ReadGpuAsync();
                process.Refresh();
                var cpu = process.TotalProcessorTime;
                double seconds = watch.Elapsed.TotalSeconds;
                double cores = (cpu - previousCpu).TotalSeconds / Math.Max(0.001, seconds - previousTime);
                samples.Add(new(seconds, sampledPhase, cores, cores * 100 / Environment.ProcessorCount,
                    process.WorkingSet64, process.PrivateMemorySize64, gpu));
                previousCpu = cpu; previousTime = seconds;
                try { await Task.Delay(900, sampling.Token); }
                catch(OperationCanceledException) { break; }
            }
        });
        try
        {
            var progress = new Progress<float>(value => { if(value >= 1) Volatile.Write(ref phase, "generating"); });
            await runtime.GenerateAsync(settings with { MaxTokens = 512 },
                [new("user", "Объясни примерно в 300 словах, как локальная языковая модель использует процессор, видеокарту и оперативную память. Дай связное объяснение с примерами.")],
                text =>
                {
                    if(firstText is null)
                    {
                        firstText = watch.Elapsed.TotalSeconds;
                        Volatile.Write(ref phase, "streaming");
                        Console.WriteLine($"First visible text: {firstText:F2}s");
                    }
                    answer.Append(text);
                }, progress, timeout.Token);
            report["generation_seconds"] = watch.Elapsed.TotalSeconds;
            report["first_visible_text_seconds"] = firstText;
            report["answer"] = answer.ToString();
            report["memory_plan"] = runtime.LastMemoryPlan;
            report["loaded_after_answer"] = runtime.IsLoaded;
            Volatile.Write(ref phase, "loaded_idle");
            await Task.Delay(6000, timeout.Token);
            Volatile.Write(ref phase, "unloading");
            await runtime.UnloadAsync(timeout.Token);
            Volatile.Write(ref phase, "unloaded_idle");
            await Task.Delay(4000, timeout.Token);
            report["unloaded"] = !runtime.IsLoaded;
            report["passed"] = answer.Length > 0 && !runtime.IsLoaded;
            if(answer.Length == 0 || runtime.IsLoaded) Environment.ExitCode = 1;
        }
        catch(Exception error)
        {
            report["passed"] = false;
            report["error"] = error.ToString();
            Environment.ExitCode = 1;
        }
        finally
        {
            sampling.Cancel(); await monitor;
            report["samples"] = samples;
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Resource probe passed: {report["passed"]}; samples: {samples.Count}");
        }
    }

    private static async Task<string> ReadGpuAsync()
    {
        try
        {
            var start = new ProcessStartInfo("nvidia-smi") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--query-gpu=utilization.gpu,memory.used,power.draw,temperature.gpu");
            start.ArgumentList.Add("--format=csv,noheader,nounits");
            using var process = Process.Start(start)!;
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output.Trim();
        }
        catch(Exception error) { return "unavailable: " + error.GetType().Name; }
    }

    private sealed record Sample(double Seconds, string Phase, double CpuCores, double CpuPercent,
        long WorkingSetBytes, long PrivateBytes, string GpuUtilization_MiB_Watts_Celsius);
}
