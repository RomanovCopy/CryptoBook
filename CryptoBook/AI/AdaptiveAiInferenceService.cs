using System.IO;

namespace CryptoBook.AI;

public sealed record AiRuntimeNotice(DateTime Time, string ResourceKey, object[] Arguments);
public interface IAiRuntimeStatusSource
{
    event EventHandler? RuntimeChanged;
    IReadOnlyList<AiRuntimeNotice> Notices { get; }
    AiRuntimeOptions? RuntimeOptions { get; }
    AiMemoryPlan? MemoryPlan { get; }
    int RequestedContextLength { get; }
    string ModelPath { get; }
    Task ResetAutoProfileAsync(string modelPath, CancellationToken cancellationToken = default);
}

/// <summary>Owns isolated native processes and never executes llama.cpp in the UI process.</summary>
public sealed partial class AdaptiveAiInferenceService : IAiInferenceService, IAiRuntimeStatusSource, IAiTokenUsageSource, IAiPerformanceService
{
    private readonly AiRuntimeProfileStore profiles;
    private readonly Func<IAiWorkerSession> createWorker;
    private readonly Func<AiResourceSnapshot> readResources;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object sync = new();
    private readonly List<AiRuntimeNotice> notices = [];
    private IAiWorkerSession? worker;
    private CancellationTokenSource? active;
    private string? loadedIdentity;
    private long epoch;
    private bool disposed, verified;
    public AdaptiveAiInferenceService(AiRuntimeProfileStore profiles) : this(profiles, () => new AiWorkerProcess()) { }
    internal AdaptiveAiInferenceService(AiRuntimeProfileStore profiles, Func<IAiWorkerSession> createWorker,
        Func<AiResourceSnapshot>? readResources = null)
    { this.profiles = profiles; this.createWorker = createWorker; this.readResources = readResources ?? AiResourceSnapshot.Read; }
    public bool IsLoaded => verified && worker?.IsAlive == true;
    public bool IsBusy { get; private set; }
    public DateTime LastUsedUtc { get; private set; } = DateTime.UtcNow;
    public AiRuntimeOptions? RuntimeOptions { get; private set; }
    public AiMemoryPlan? MemoryPlan { get; private set; }
    public int RequestedContextLength { get; private set; }
    public string ModelPath { get; private set; } = "";
    public event EventHandler? RuntimeChanged;
    public event Action<AiTokenUsage>? TokenUsageChanged;
    public IReadOnlyList<AiRuntimeNotice> Notices { get { lock(sync) return notices.ToArray(); } }

    public Task GenerateAsync(AiModelSettings settings, IReadOnlyList<AiTurn> conversation,
        Action<string> onText, IProgress<float>? loadProgress, CancellationToken cancellationToken, Action<string>? onReasoning = null)
        => RunAsync(settings, conversation, onText, loadProgress, cancellationToken, onReasoning);

    public Task LoadAsync(AiModelSettings settings, IProgress<float>? loadProgress, CancellationToken cancellationToken)
        => RunAsync(settings, [], _ => { }, loadProgress, cancellationToken, preload: true);

    private async Task RunAsync(AiModelSettings settings, IReadOnlyList<AiTurn> conversation,
        Action<string> onText, IProgress<float>? loadProgress, CancellationToken cancellationToken,
        Action<string>? onReasoning = null, bool preload = false)
    {
        settings.Validate();
        AiWorkerProtocol.ValidateConversation(settings, conversation);
        long startedEpoch;
        lock(sync) startedEpoch = epoch;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            lock(sync)
            {
                if(startedEpoch != epoch) throw new OperationCanceledException();
                active = linked;
            }
            IsBusy = true;
            RequestedContextLength = settings.ContextLength;
            ModelPath = settings.ModelPath;
            var file = new FileInfo(settings.ModelPath);
            bool allowBoost = AiPerformancePolicy.IsAllowed(settings, readResources());
            string identity = $"{file.FullName.ToUpperInvariant()}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{settings.ContextLength}|{settings.MemoryMode}|{settings.GpuLayers}|{settings.AutoReduceContext}|{settings.EffectivePerformanceMode}|{allowBoost}";
            AiModelInspection? inspection = null;
            string? key = null;
            var failed = new HashSet<AiRuntimeOptions>();
            if(!IsLoaded || loadedIdentity != identity)
            {
                await StopWorkerAsync().ConfigureAwait(false);
                lock(sync) notices.Clear();
                RuntimeOptions = null;
                Emit("AI.AutoInspect");
                inspection = await InspectAsync(settings, linked.Token).ConfigureAwait(false);
                MemoryPlan = inspection.Plan;
                key = AiRuntimeProfileStore.Key(settings, inspection.HardwareKey);
                Emit("AI.AutoResources", MiB(inspection.Plan.AvailableRamBytes), MiB(inspection.Plan.AvailableGpuBytes),
                    string.IsNullOrEmpty(inspection.Plan.GpuName) ? "AI.AutoNoGpu" : inspection.Plan.GpuName);
                await SelectAsync(settings, inspection, key, failed, loadProgress, linked.Token).ConfigureAwait(false);
                loadedIdentity = identity;
            }
            while(true)
            {
                bool sentOutput = false;
                try
                {
                    if(!preload) Emit("AI.AutoGenerating");
                    var request = new AiWorkerRequest(preload ? "load" : "generate", settings, RuntimeOptions!, conversation.Select(AiWorkerTurn.From).ToArray());
                    await worker!.ExecuteAsync(request, text => { sentOutput = true; onText(text); }, loadProgress, linked.Token,
                        text => { sentOutput = true; onReasoning?.Invoke(text); },
                        usage => TokenUsageChanged?.Invoke(usage)).ConfigureAwait(false);
                    Emit("AI.AutoComplete");
                    break;
                }
                catch(Exception error) when((IsRetryable(error) || RuntimeOptions!.PerformanceBoost && IsBoostUnavailable(error)) && !linked.IsCancellationRequested)
                {
                    var failedOptions = RuntimeOptions!;
                    bool boostUnavailable = failedOptions.PerformanceBoost && IsBoostUnavailable(error);
                    failed.Add(failedOptions);
                    await StopWorkerAsync().ConfigureAwait(false);
                    Emit("AI.AutoAttemptFailed", FailureReason(error));
                    if(settings.MemoryMode == AiMemoryMode.Automatic) RejectProfile(settings.ModelPath);
                    if(settings.MemoryMode != AiMemoryMode.Automatic && !boostUnavailable || sentOutput)
                    {
                        Emit(sentOutput ? "AI.AutoPartialReply" : "AI.AutoManualFailure");
                        throw new AiException("AI.WorkerFailed");
                    }
                    Emit(boostUnavailable ? "AI.PerformanceFallback" : "AI.AutoRecovering");
                    if(inspection is null)
                    {
                        inspection = await InspectAsync(settings, linked.Token).ConfigureAwait(false);
                        MemoryPlan = inspection.Plan;
                        key = AiRuntimeProfileStore.Key(settings, inspection.HardwareKey);
                    }
                    await SelectAsync(settings, inspection, key!, failed, loadProgress, linked.Token).ConfigureAwait(false);
                    loadedIdentity = identity;
                }
                catch(AiException error) when(error.ResourceKey == "AI.ContextFull" && RuntimeOptions!.ContextLength < settings.ContextLength)
                { throw new AiException("AI.AutoContextFull"); }
            }
        }
        catch(AiRepetitionException)
        {
            // A controlled stop is not a failed hardware configuration. Keep the verified
            // worker/profile and never regenerate a partially delivered answer automatically.
            Emit("AI.RepetitionStopped"); throw;
        }
        catch(OperationCanceledException)
        {
            await StopWorkerAsync().ConfigureAwait(false); Emit("AI.AutoCancelled"); throw;
        }
        catch
        {
            await StopWorkerAsync().ConfigureAwait(false); Emit("AI.AutoFailed"); throw;
        }
        finally
        {
            lock(sync) active = null;
            IsBusy = false; LastUsedUtc = DateTime.UtcNow; gate.Release();
            RuntimeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<AiModelInspection> InspectAsync(AiModelSettings settings, CancellationToken token, bool protectResources = false)
    {
        await StopWorkerAsync().ConfigureAwait(false); worker = createWorker();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            return await worker.ExecuteAsync(new("inspect", settings with { SystemPrompt = "" }, new(settings.ContextLength, 0, CpuBackend: true), [], protectResources),
                _ => { }, null, timeout.Token).ConfigureAwait(false) ?? throw new AiException("AI.InspectionFailed");
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested) { throw new AiException("AI.InspectionTimeout"); }
        catch(AiWorkerException error) { Emit("AI.AutoAttemptFailed", FailureReason(error)); throw new AiException("AI.InspectionFailed"); }
        catch(AiException error) when(error.ResourceKey is "AI.LoadFailed" or "AI.GenerationFailed")
        { Emit("AI.AutoAttemptFailed", error.ResourceKey); throw new AiException("AI.InspectionFailed"); }
        finally { await StopWorkerAsync().ConfigureAwait(false); }
    }

    private async Task SelectAsync(AiModelSettings settings, AiModelInspection inspection, string key,
        HashSet<AiRuntimeOptions> failed, IProgress<float>? progress, CancellationToken token, bool calibration = false)
    {
        var candidates = AiRuntimeCandidates.Create(settings, inspection)
            .Select(options => calibration ? options : AiPerformancePolicy.ApplyMode(options, settings, Environment.ProcessorCount)).ToList();
        AiRuntimeProfile? cached = null;
        if(settings.MemoryMode == AiMemoryMode.Automatic)
        {
            try { cached = profiles.Find(key); }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException) { Emit("AI.AutoCacheUnavailable"); }
            if(cached is not null && !cached.Options.PerformanceBoost &&
               cached.Options.PowerSaving == (settings.EffectivePerformanceMode == AiPerformanceMode.PowerSaving) &&
               cached.Options.GpuLayers >= 0 && cached.Options.GpuLayers <= inspection.Plan.GpuLayers &&
               cached.Options.ContextLength <= settings.ContextLength &&
               (settings.AutoReduceContext || cached.Options.ContextLength == settings.ContextLength))
            {
                candidates.Remove(cached.Options); candidates.Insert(0, cached.Options);
                if(candidates.Count > 13) candidates.RemoveAt(1);
            }
        }
        if(!calibration && AiPerformancePolicy.IsAllowed(settings, readResources()))
        {
            try
            {
                var tuned = profiles.Find(PerformanceKey(settings, inspection.HardwareKey));
                if(tuned?.Performance is { IsValid: true, BoostSelected: true } &&
                    tuned.Options.PerformanceBoost && tuned.Options.ContextLength == settings.ContextLength &&
                    (tuned.Options.GpuLayers == inspection.Plan.GpuLayers ||
                     tuned.Options.GpuLayers >= 0 && tuned.Options.GpuLayers <= inspection.Plan.GpuLayers))
                { candidates.Insert(0, tuned.Options); Emit("AI.PerformanceUsing"); }
            }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException) { Emit("AI.AutoCacheUnavailable"); }
        }
        int attempt = failed.Count;
        int total = Math.Min(13, failed.Count + candidates.Count(candidate => !failed.Contains(candidate)));
        foreach(var candidate in candidates.Where(candidate => !failed.Contains(candidate)))
        {
            if(attempt >= 13) break;
            token.ThrowIfCancellationRequested();
            await StopWorkerAsync().ConfigureAwait(false);
            RuntimeOptions = candidate;
            Emit(cached?.Options == candidate ? "AI.AutoCached" : "AI.AutoAttempt", ++attempt, total,
                candidate.ContextLength, candidate.GpuLayers, candidate.BatchSize, candidate.UBatchSize,
                candidate.FlashAttention == false ? "off" : "auto");
            worker = createWorker();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                try
                {
                    await worker.ExecuteAsync(new("probe", settings with { SystemPrompt = "" }, candidate, [], ProtectResources: calibration), _ => { }, progress, timeout.Token).ConfigureAwait(false);
                }
                catch(OperationCanceledException) when(!token.IsCancellationRequested) { throw new AiWorkerException("Timeout120s"); }
                verified = true;
                Emit("AI.AutoVerified");
                if(candidate.ContextLength < settings.ContextLength) Emit("AI.AutoReducedContext", settings.ContextLength, candidate.ContextLength);
                if(candidate.GpuLayers == 0) Emit("AI.AutoCpuSelected");
                if(candidate.FlashAttention == false) Emit("AI.AutoFlashDisabled");
                if(!calibration && !candidate.PerformanceBoost && settings.MemoryMode == AiMemoryMode.Automatic)
                {
                    try { profiles.Save(new(key, settings.ModelPath, candidate, DateTime.UtcNow)); Emit("AI.AutoSaved"); }
                    catch(Exception error) when(error is IOException or UnauthorizedAccessException) { Emit("AI.AutoCacheUnavailable"); }
                }
                return;
            }
            catch(Exception error) when((IsRetryable(error) || candidate.PerformanceBoost && IsBoostUnavailable(error)) && !token.IsCancellationRequested)
            {
                failed.Add(candidate); await StopWorkerAsync().ConfigureAwait(false); Emit("AI.AutoAttemptFailed", FailureReason(error));
                if(candidate.PerformanceBoost && IsBoostUnavailable(error)) Emit("AI.PerformanceFallback");
                if(cached?.Options == candidate) RejectProfile(settings.ModelPath);
                if(settings.MemoryMode != AiMemoryMode.Automatic) Emit("AI.AutoManualFailure");
            }
        }
        throw new AiException(settings.MemoryMode == AiMemoryMode.Automatic ? "AI.AutoNoConfiguration" : "AI.WorkerFailed");
    }

    private static bool IsRetryable(Exception error) => error is AiWorkerException || error is AiException
        { ResourceKey: "AI.LoadFailed" or "AI.GenerationFailed" or "AI.ImageLoadFailed" };
    private static bool IsBoostUnavailable(Exception error) => error is AiException
        { ResourceKey: "AI.ResourceGuardUnavailable" or "AI.PerformanceConsentRequired" or "AI.PerformancePowerChanged" };
    private static string FailureReason(Exception error) => error is AiWorkerException ? error.Message switch
    {
        "Timeout120s" => "AI.AutoTimeout",
        "ConnectionLost" or "Protocol" => "AI.AutoDisconnected",
        "JobCreate" or "JobConfigure" or "JobAssign" or "ProcessStart" => "AI.AutoStartFailed",
        _ => error.Message
    } : ((AiException)error).ResourceKey;
    private static long MiB(long bytes) => bytes / (1024 * 1024);
    private void RejectProfile(string modelPath)
    {
        try { profiles.Remove(modelPath); Emit("AI.AutoProfileRejected"); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) { Emit("AI.AutoCacheUnavailable"); }
    }
    private void Emit(string key, params object[] args)
    {
        lock(sync)
        {
            if(notices.Count == 80) notices.RemoveAt(0);
            notices.Add(new(DateTime.UtcNow, key, args));
        }
        RuntimeChanged?.Invoke(this, EventArgs.Empty);
    }
    private void StopWorker()
    {
        worker?.Dispose(); worker = null; verified = false; loadedIdentity = null;
    }
    private async Task StopWorkerAsync()
    {
        var stopping = worker;
        worker = null; verified = false; loadedIdentity = null;
        if(stopping is null) return;
        try { await stopping.StopAsync().ConfigureAwait(false); }
        finally { stopping.Dispose(); }
    }
    public async Task UnloadAsync(CancellationToken cancellationToken = default)
    {
        lock(sync) { epoch++; active?.Cancel(); }
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await StopWorkerAsync().ConfigureAwait(false); Emit("AI.AutoUnloaded"); }
        finally { gate.Release(); }
    }
    public async Task ResetContextAsync(CancellationToken cancellationToken = default)
    {
        long startedEpoch;
        lock(sync) startedEpoch = epoch;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            lock(sync)
            {
                if(disposed || startedEpoch != epoch || !IsLoaded) return;
                active = linked;
            }
            IsBusy = true;
            await worker!.ExecuteAsync(new("reset", new() { ModelPath = ModelPath }, RuntimeOptions!, []),
                _ => { }, null, linked.Token).ConfigureAwait(false);
        }
        catch
        {
            await StopWorkerAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            lock(sync) active = null;
            IsBusy = false;
            gate.Release();
            RuntimeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public async Task ResetAutoProfileAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        lock(sync) { epoch++; active?.Cancel(); }
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopWorkerAsync().ConfigureAwait(false);
            profiles.Remove(modelPath);
            Emit("AI.AutoReset");
        }
        finally { gate.Release(); }
    }
    public void Dispose()
    {
        lock(sync) { epoch++; active?.Cancel(); }
        gate.Wait();
        try { disposed = true; StopWorker(); }
        finally { gate.Release(); }
    }
}
