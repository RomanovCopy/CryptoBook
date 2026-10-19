using CryptoBook.AI;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

internal static class AdaptiveProbe
{
    public static async Task RunAsync(string model, string directory, bool cpuOnly = false)
    {
        Directory.CreateDirectory(directory);
        string cache = Path.Combine(directory, "profiles.json");
        var settings = new AiModelSettings { ModelPath = model, MaxTokens = 64, Temperature = 0,
            MemoryMode = cpuOnly ? AiMemoryMode.CpuOnly : AiMemoryMode.Automatic };
        var report = new Dictionary<string, object?> { ["model"] = model };
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        var watch = Stopwatch.StartNew();
        try
        {
            using(var runtime = new AdaptiveAiInferenceService(new AiRuntimeProfileStore(cache)))
            {
                int notices = 0;
                runtime.RuntimeChanged += (_, _) =>
                {
                    foreach(var notice in runtime.Notices.Skip(notices)) Console.WriteLine(notice.ResourceKey + " " + string.Join(", ", notice.Arguments));
                    notices = runtime.Notices.Count;
                };
                var answer = new StringBuilder();
                await runtime.GenerateAsync(settings, [new("user", "Reply with one word: ready")], text => answer.Append(text), null, timeout.Token);
                Check(answer.Length > 0 && runtime.IsLoaded, "No generated text or loaded worker.");
                report["answer"] = answer.ToString();
                report["runtime_options"] = runtime.RuntimeOptions;
                report["memory_plan"] = runtime.MemoryPlan;
                report["initial_seconds"] = watch.Elapsed.TotalSeconds;
                using var cancel = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                bool stopped = false;
                try
                {
                    await runtime.GenerateAsync(settings, [new("user", "Write a detailed story.")], _ => cancel.Cancel(), null, cancel.Token);
                }
                catch(OperationCanceledException) when(!timeout.IsCancellationRequested) { stopped = true; }
                Check(stopped && !runtime.IsLoaded, "Cancellation did not close the worker.");
                report["cancellation_releases_worker"] = true;
            }
            using(var cachedRuntime = new AdaptiveAiInferenceService(new AiRuntimeProfileStore(cache)))
            {
                var answer = new StringBuilder();
                await cachedRuntime.GenerateAsync(settings, [new("user", "Say ready.")], text => answer.Append(text), null, timeout.Token);
                Check(answer.Length > 0 && (cpuOnly || cachedRuntime.Notices.Any(notice => notice.ResourceKey == "AI.AutoCached")), "Verified cache was not used.");
                report[cpuOnly ? "cpu_reload_passed" : "cached_profile_checked_and_used"] = true;
                await cachedRuntime.UnloadAsync(timeout.Token);
                Check(!cachedRuntime.IsLoaded, "Unload left a model loaded.");
                report["unloaded"] = true;
            }
            using(var worker = new AiWorkerProcess())
            {
                var inspection = await worker.ExecuteAsync(new("inspect", settings, new(4096, 0, CpuBackend: true), []), _ => { }, null, timeout.Token);
                Check(inspection is not null && worker.ProcessId is not null, "No isolated inspection worker.");
                using var child = Process.GetProcessById(worker.ProcessId!.Value);
                child.Kill();
                await child.WaitForExitAsync(timeout.Token);
                bool detected = false;
                try { await worker.ExecuteAsync(new("inspect", settings, new(4096, 0, CpuBackend: true), []), _ => { }, null, timeout.Token); }
                catch(AiWorkerException) { detected = true; }
                Check(detected, "A killed worker was not detected.");
                report["worker_exit_does_not_kill_parent"] = true;
            }
            report["elapsed_seconds"] = watch.Elapsed.TotalSeconds;
            using(var parent = Process.GetCurrentProcess())
            {
                bool nativeLoaded = parent.Modules.Cast<ProcessModule>().Any(module => module.ModuleName is "llama.dll" or "ggml-vulkan.dll" or "mtmd.dll");
                Check(!nativeLoaded, "Native engine libraries were loaded in the parent process.");
                report["parent_has_no_native_engine"] = true;
            }
            var ownerStart = new ProcessStartInfo(Environment.ProcessPath!)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            ownerStart.ArgumentList.Add("--worker-owner"); ownerStart.ArgumentList.Add(model);
            using(var owner = Process.Start(ownerStart)!)
            {
                try
                {
                    string childId = await owner.StandardOutput.ReadLineAsync(timeout.Token) ?? throw new Exception("Owner did not report its child.");
                    int id = int.Parse(childId);
                    using var nativeChild = Process.GetProcessById(id);
                    owner.Kill(entireProcessTree: false);
                    await owner.WaitForExitAsync(timeout.Token);
                    using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try { await nativeChild.WaitForExitAsync(exitTimeout.Token); }
                    catch { nativeChild.Kill(entireProcessTree: true); throw; }
                    report["parent_exit_terminates_worker"] = true;
                }
                finally { if(!owner.HasExited) { owner.Kill(entireProcessTree: true); await owner.WaitForExitAsync(timeout.Token); } }
            }
            report["passed"] = true;
        }
        catch(Exception error)
        {
            report["passed"] = false;
            report["error"] = error.ToString();
            Environment.ExitCode = 1;
        }
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
    }
    private static void Check(bool value, string message) { if(!value) throw new Exception(message); }
}
