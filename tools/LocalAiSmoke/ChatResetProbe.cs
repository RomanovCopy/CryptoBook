using CryptoBook.AI;
using System.IO;
using System.Text;
using System.Text.Json;

internal static class ChatResetProbe
{
    internal static async Task RunAsync(string modelPath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = timeout.Token;
        var report = new Dictionary<string, object?>();
        void Check(bool condition, string name)
        {
            if(!condition) throw new InvalidOperationException(name);
            report[name] = true;
        }
        try
        {
            var settings = new AiModelSettings
            {
                ModelPath = Path.GetFullPath(modelPath), MemoryMode = AiMemoryMode.Manual,
                GpuLayers = 0, ContextLength = 512, MaxTokens = 16, Temperature = 0,
                ReuseContext = true, PerformanceMode = AiPerformanceMode.Optimal,
                AllowPerformanceBoost = true, BoostOnlyOnAc = false
            };
            var options = new AiRuntimeOptions(512, 0, BatchSize: 32, UBatchSize: 32, CpuBackend: true,
                Threads: 2, BatchThreads: 2, PerformanceBoost: true);
            AiModelInspection inspection;
            using(var inspector = new AiWorkerProcess())
            {
                inspection = await inspector.ExecuteAsync(new("inspect", settings with { MemoryMode = AiMemoryMode.Automatic },
                    options with { PerformanceBoost = false }, [], ProtectResources: true), _ => { }, null, token)
                    ?? throw new InvalidOperationException("The memory planner returned no model inspection.");
                await inspector.StopAsync();
            }
            settings = settings with { GpuLayers = inspection.Plan.GpuLayers };
            options = options with { GpuLayers = settings.GpuLayers, CpuBackend = settings.GpuLayers == 0 };
            report["gpu_layers"] = settings.GpuLayers;
            using(var oldReset = new AiWorkerProcess())
            {
                try
                {
                    await oldReset.ExecuteAsync(new("reset", new() { ModelPath = settings.ModelPath }, options, []), _ => { }, null, token);
                    throw new InvalidOperationException("The original reset unexpectedly passed consent checks.");
                }
                catch(AiException error) when(error.ResourceKey == "AI.PerformanceConsentRequired")
                {
                    Check(!oldReset.IsAlive, "original_reset_reproduces_consent_failure_and_worker_exit");
                }
            }
            using var worker = new AiWorkerProcess();
            await worker.ExecuteAsync(new("load", settings, options, []), _ => { }, null, token);
            Check(worker.IsAlive, "real_model_loaded_in_owned_worker");
            int? processId = worker.ProcessId;
            var resetOptions = options with { PerformanceBoost = false };
            await worker.ExecuteAsync(new("reset", new() { ModelPath = settings.ModelPath }, resetOptions, []), _ => { }, null, token);
            Check(worker.IsAlive && worker.ProcessId == processId, "reset_keeps_same_native_worker_alive");
            var resources = AiResourceSnapshot.Read();
            if(resources.HasHeadroom && resources.CommitLimit >= 512L * 1024 * 1024)
            {
                var answer = new StringBuilder();
                int weightLoadProgress = 0;
                var progress = new AiInlineProgress<float>(value => { if(value < 1) weightLoadProgress++; });
                await worker.ExecuteAsync(new("generate", settings, options,
                    [new("user", "Reply with one word: ready", [])]), text => answer.Append(text), progress, token);
                Check(answer.Length > 0, "generation_after_reset_succeeds");
                Check(weightLoadProgress == 0, "generation_after_reset_does_not_reload_weights");
                Check(worker.IsAlive && worker.ProcessId == processId, "generation_reuses_same_native_worker");
                using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
                int partialFragments = 0;
                try
                {
                    await worker.ExecuteAsync(new("generate", settings with { MaxTokens = 128 }, options,
                        [new("user", "List the numbers from 1 to 200, one per line, without skipping any.", [])]),
                        _ => { partialFragments++; stop.Cancel(); }, null, stop.Token);
                    throw new InvalidOperationException("The generation did not acknowledge cancellation.");
                }
                catch(AiWorkerCancelledException)
                {
                    Check(partialFragments > 0, "stop_preserves_received_partial_output");
                    Check(worker.IsAlive && worker.ProcessId == processId, "stop_keeps_same_native_worker_alive");
                }
                answer.Clear(); weightLoadProgress = 0;
                await worker.ExecuteAsync(new("generate", settings, options,
                    [new("user", "Reply with one word: ready", [])]), text => answer.Append(text), progress, token);
                Check(answer.Length > 0 && weightLoadProgress == 0, "next_request_after_stop_succeeds_without_reloading_weights");
                Check(worker.IsAlive && worker.ProcessId == processId, "next_request_after_stop_reuses_native_worker");
            }
            else report["generation_after_reset_skipped"] = "Resource pressure: insufficient reserve for a guarded inference request.";
            await worker.StopAsync();
            Check(!worker.IsAlive, "explicit_unload_stops_worker");
            report["success"] = true;
        }
        catch(Exception error)
        {
            report["success"] = false;
            report["error"] = error.ToString();
            Environment.ExitCode = 1;
        }
        finally
        {
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
