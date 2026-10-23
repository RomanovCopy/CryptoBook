namespace CryptoBook.AI;

public sealed partial class AdaptiveAiInferenceService
{
    public AiPerformanceResult? GetLastPerformance(AiModelSettings settings)
    {
        try { return profiles.LastPerformance(settings); }
        catch(Exception error) when(error is System.IO.IOException or UnauthorizedAccessException) { return null; }
    }
    internal static string PerformanceKey(AiModelSettings settings, string hardware) =>
        AiRuntimeProfileStore.Key(settings, hardware) + $"|performance|{settings.MemoryMode}|{settings.GpuLayers}";

    public async Task<AiPerformanceResult> TestPerformanceAsync(AiModelSettings settings, CancellationToken cancellationToken)
    {
        settings.Validate();
        if(!AiPerformancePolicy.IsAllowed(settings, readResources())) throw new AiException("AI.PerformanceConsentRequired");
        if(!readResources().HasHeadroom) throw new AiException("AI.ResourcePressure");
        long startedEpoch;
        lock(sync) startedEpoch = epoch;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            lock(sync)
            {
                if(epoch != startedEpoch) throw new OperationCanceledException();
                active = linked; notices.Clear();
            }
            IsBusy = true;
            await StopWorkerAsync().ConfigureAwait(false);
            RuntimeOptions = null;
            RequestedContextLength = settings.ContextLength; ModelPath = settings.ModelPath;
            Emit("AI.PerformancePreparing");
            var inspection = await InspectAsync(settings, linked.Token, protectResources: true).ConfigureAwait(false);
            MemoryPlan = inspection.Plan;
            // Every measured candidate retains the user's full context and the existing GPU reserve.
            var exact = settings with { AutoReduceContext = false };
            var beforeProbe = readResources();
            await SelectAsync(exact, inspection, AiRuntimeProfileStore.Key(exact, inspection.HardwareKey), [], null,
                linked.Token, calibration: true).ConfigureAwait(false);
            var candidates = AiPerformancePolicy.Candidates(RuntimeOptions!, Environment.ProcessorCount);
            await StopWorkerAsync().ConfigureAwait(false);
            await WaitForResourceRecoveryAsync(beforeProbe, linked.Token).ConfigureAwait(false);
            AiPerformanceMeasurement? baseline = null, selected = null;
            AiRuntimeOptions chosen = candidates[0];
            for(int index = 0; index < candidates.Count; index++)
            {
                linked.Token.ThrowIfCancellationRequested();
                if(!AiPerformancePolicy.IsAllowed(settings, readResources())) throw new AiException("AI.PerformanceConsentRequired");
                if(!readResources().HasHeadroom) throw new AiException("AI.ResourcePressure");
                var candidate = candidates[index];
                var beforeTrial = readResources();
                RuntimeOptions = candidate;
                Emit("AI.PerformanceTrial", index + 1, candidates.Count, candidate.Threads, candidate.BatchThreads);
                worker = createWorker();
                try
                {
                    AiPerformanceMeasurement? measured = null;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(90));
                    await worker.ExecuteAsync(new("benchmark", settings with { SystemPrompt = "" }, candidate, [], true),
                        _ => { }, null, timeout.Token, onMeasurement: value => measured = value).ConfigureAwait(false);
                    if(measured is not { IsValid: true }) throw new AiException("AI.PerformanceTooShort");
                    Emit("AI.PerformanceMeasured", measured.PromptTokensPerSecond, measured.GenerationTokensPerSecond,
                        measured.PeakRamBytes / (1024 * 1024), measured.PeakGpuBytes / (1024 * 1024));
                    baseline ??= measured;
                    if(selected is null || measured.WorkSeconds < selected.WorkSeconds)
                    { selected = measured; chosen = candidate; }
                }
                catch(OperationCanceledException) when(!linked.IsCancellationRequested)
                {
                    if(baseline is null) throw new AiException("AI.PerformanceTrialTimeout");
                    Emit("AI.PerformanceTrialFailed");
                }
                catch(Exception error) when(baseline is not null && (IsRetryable(error) || error is AiException { ResourceKey: "AI.PerformanceTooShort" }))
                { Emit("AI.PerformanceTrialFailed"); }
                finally
                {
                    await StopWorkerAsync().ConfigureAwait(false);
                    if(!linked.IsCancellationRequested)
                        await WaitForResourceRecoveryAsync(beforeTrial, linked.Token).ConfigureAwait(false);
                }
            }
            linked.Token.ThrowIfCancellationRequested();
            // A gain below 5% is treated as measurement noise. Never opt into extra load without a useful gain.
            bool improved = chosen.PerformanceBoost && baseline!.WorkSeconds / selected!.WorkSeconds >= 1.05;
            if(!improved) { chosen = candidates[0]; selected = baseline; }
            var result = new AiPerformanceResult(baseline!, selected!, improved, DateTime.UtcNow);
            if(!AiPerformancePolicy.IsAllowed(settings, readResources())) throw new AiException("AI.PerformanceConsentRequired");
            profiles.Save(new(PerformanceKey(settings, inspection.HardwareKey), settings.ModelPath, chosen, DateTime.UtcNow, result));
            RuntimeOptions = chosen;
            Emit(improved ? "AI.PerformanceSelected" : "AI.PerformanceNoGain", (result.Speedup - 1) * 100);
            return result;
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        { Emit("AI.PerformanceTimeout"); throw new AiException("AI.PerformanceTimeout"); }
        finally
        {
            try { await StopWorkerAsync().ConfigureAwait(false); }
            finally
            {
                lock(sync) active = null;
                IsBusy = false; LastUsedUtc = DateTime.UtcNow; gate.Release();
                RuntimeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    private async Task WaitForResourceRecoveryAsync(AiResourceSnapshot before, CancellationToken token)
    {
        // WDDM/system commit accounting can lag process exit. Do not size a fresh worker against that transient low budget.
        const long tolerance = 512L * 1024 * 1024;
        long deadline = Environment.TickCount64 + 3000;
        while(Environment.TickCount64 < deadline)
        {
            token.ThrowIfCancellationRequested();
            var current = readResources();
            if(current.AvailableRam >= before.AvailableRam - tolerance &&
                current.AvailableCommit >= before.AvailableCommit - tolerance) return;
            await Task.Delay(100, token).ConfigureAwait(false);
        }
        // Other applications may have allocated memory meanwhile; the next guard uses the new actual budget.
    }
}
