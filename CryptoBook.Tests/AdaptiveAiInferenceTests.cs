using CryptoBook.AI;
using System.IO;
using System.Text;
using Xunit;

namespace CryptoBook.Tests;

public sealed class AdaptiveAiInferenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AutoAI-" + Guid.NewGuid().ToString("N"));
    private readonly List<AiWorkerRequest> requests = [];
    private readonly List<FakeWorker> workers = [];
    private AiModelSettings Settings => new() { ModelPath = Path.Combine(directory, "model.gguf") };
    private AiRuntimeProfileStore Store => new(Path.Combine(directory, "profiles.json"));
    private static AiModelInspection Inspection => new(new(4, 8, 500_000_000, 200_000_000, 300_000_000,
        2_000_000_000, 4_000_000_000, "Test GPU"), new() { ["general.architecture"] = "llama" }, "hardware-driver-1");

    public AdaptiveAiInferenceTests()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Settings.ModelPath, "GGUF"u8.ToArray());
    }
    private AdaptiveAiInferenceService Create(Func<AiWorkerRequest, Action<string>, CancellationToken, Task>? behavior = null,
        AiRuntimeProfileStore? store = null, string? reasoning = null) => new(store ?? Store, () =>
    {
        var worker = new FakeWorker(async (request, text, onReasoning, token) =>
        {
            requests.Add(request);
            if(request.Command == "inspect") return Inspection;
            if(request.Command == "generate" && reasoning is not null) onReasoning?.Invoke(reasoning);
            if(behavior is not null) await behavior(request, text, token);
            else if(request.Command == "generate") text("ready");
            return null;
        });
        workers.Add(worker);
        return worker;
    });
    private static Task Generate(AdaptiveAiInferenceService runtime, AiModelSettings settings, Action<string>? text = null,
        CancellationToken token = default) => runtime.GenerateAsync(settings, [new("user", "private request")], text ?? (_ => { }), null, token);

    [Fact]
    public async Task NativeCrashDuringProbe_RetriesWithoutFlashAttention_AndKeepsGpu()
    {
        using var runtime = Create((request, text, _) =>
        {
            if(request.Command == "probe" && request.Options.FlashAttention is null) throw new AiWorkerException("0xC0000409");
            if(request.Command == "generate") text("ready");
            return Task.CompletedTask;
        });
        await Generate(runtime, Settings);
        Assert.True(runtime.IsLoaded);
        Assert.False(runtime.RuntimeOptions!.FlashAttention);
        Assert.Equal(4, runtime.RuntimeOptions.GpuLayers);
        Assert.Equal(2, requests.Count(request => request.Command == "probe"));
        Assert.Contains(runtime.Notices, notice => notice.Arguments.Contains("0xC0000409"));
        Assert.DoesNotContain("private request", File.ReadAllText(Path.Combine(directory, "profiles.json")));
        Assert.DoesNotContain(runtime.Notices, notice => notice.Arguments.Contains("private request"));
    }

    [Fact]
    public async Task ManualFailure_DoesNotChangeGpuOrContext()
    {
        using var runtime = Create((_, _, _) => throw new AiWorkerException("0xC0000409"));
        var settings = Settings with { MemoryMode = AiMemoryMode.Manual, GpuLayers = 4, ContextLength = 8192 };
        var error = await Assert.ThrowsAsync<AiException>(() => Generate(runtime, settings));
        Assert.Equal("AI.WorkerFailed", error.ResourceKey);
        Assert.Single(requests, request => request.Command == "probe");
        Assert.Equal(8192, runtime.RuntimeOptions!.ContextLength);
        Assert.Equal(4, runtime.RuntimeOptions.GpuLayers);
        Assert.False(runtime.IsLoaded);
    }

    [Fact]
    public async Task SavedProfile_IsCheckedFirst_AndLoadedWorkerIsReused()
    {
        var selected = new AiRuntimeOptions(2048, 2, 128, 32, false);
        Store.Save(new(AiRuntimeProfileStore.Key(Settings, Inspection.HardwareKey), Settings.ModelPath, selected, DateTime.UtcNow));
        using var runtime = Create();
        await Generate(runtime, Settings);
        await Generate(runtime, Settings);
        Assert.Equal(selected, Assert.Single(requests, request => request.Command == "probe").Options);
        Assert.Single(requests, request => request.Command == "inspect");
        Assert.Equal(2, requests.Count(request => request.Command == "generate"));
        Assert.Contains(runtime.Notices, notice => notice.ResourceKey == "AI.AutoCached");
        Assert.Contains(runtime.Notices, notice => notice.ResourceKey == "AI.AutoReducedContext");
    }

    [Fact]
    public async Task ReasoningSetting_IsSentForEachRequestEvenWhenWorkerIsReused()
    {
        using var runtime = Create();
        foreach(bool enabled in new[] { false, true, false })
            await Generate(runtime, Settings with { UseReasoning = enabled });
        Assert.Single(requests, request => request.Command == "inspect");
        Assert.Equal(new[] { false, true, false }, requests.Where(request => request.Command == "generate")
            .Select(request => request.Settings.UseReasoning));
    }

    [Fact]
    public async Task CrashAfterReasoning_KeepsAnalysisSeparateAndDoesNotRepeatTheRequest()
    {
        using var runtime = Create((request, _, _) => request.Command == "generate"
            ? throw new AiWorkerException("0xC0000409") : Task.CompletedTask, reasoning: "private analysis");
        var reasoning = new StringBuilder();
        var answer = new StringBuilder();
        await Assert.ThrowsAsync<AiException>(() => runtime.GenerateAsync(Settings, [new("user", "request")],
            text => answer.Append(text), null, CancellationToken.None, text => reasoning.Append(text)));
        Assert.Equal("private analysis", reasoning.ToString());
        Assert.Empty(answer.ToString());
        Assert.Single(requests, request => request.Command == "generate");
        Assert.Contains(runtime.Notices, notice => notice.ResourceKey == "AI.AutoPartialReply");
        Assert.DoesNotContain(runtime.Notices, notice => notice.Arguments.Contains("private analysis"));
    }

    [Fact]
    public async Task Cancellation_StopsWorker_DoesNotSaveUnverifiedProfile_AndAllowsRetry()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool hold = true;
        using var runtime = Create(async (request, _, token) =>
        {
            if(request.Command == "probe" && hold) { started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
        });
        using var cancel = new CancellationTokenSource();
        var generating = Generate(runtime, Settings, token: cancel.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generating);
        Assert.False(runtime.IsBusy);
        Assert.False(runtime.IsLoaded);
        Assert.False(File.Exists(Path.Combine(directory, "profiles.json")));
        Assert.All(workers, worker => Assert.False(worker.IsAlive));
        hold = false;
        await Generate(runtime, Settings);
        Assert.True(runtime.IsLoaded);
    }

    [Fact]
    public async Task RepetitionStop_KeepsVerifiedWorkerAndProfile_WithoutAutomaticRetry()
    {
        bool repeat = true;
        using var runtime = Create((request, text, _) =>
        {
            if(request.Command == "generate")
            {
                text("useful answer");
                if(repeat) throw new AiRepetitionException(new(false, 13));
            }
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<AiRepetitionException>(() => Generate(runtime, Settings));
        Assert.True(runtime.IsLoaded);
        Assert.False(runtime.IsBusy);
        Assert.Single(requests, request => request.Command == "generate");
        Assert.Contains(runtime.Notices, notice => notice.ResourceKey == "AI.RepetitionStopped");
        Assert.DoesNotContain(runtime.Notices, notice => notice.ResourceKey is "AI.AutoFailed" or "AI.AutoRecovering");
        Assert.True(File.Exists(Path.Combine(directory, "profiles.json")));
        repeat = false;
        await Generate(runtime, Settings with { RepetitionProtection = false });
        Assert.Single(requests, request => request.Command == "probe");
        Assert.Single(requests, request => request.Command == "inspect");
        Assert.False(requests.Last().Settings.RepetitionProtection);
    }

    [Fact]
    public async Task RepetitionStop_RoundTripsOnlyChannelAndOffsetOverThePipe()
    {
        using var stream = new MemoryStream();
        var expected = new AiRepetitionStop(true, 42);
        await AiWorkerProtocol.WriteAsync(stream, new AiWorkerEvent("repetition", Repetition: expected), CancellationToken.None);
        stream.Position = 0;
        var decoded = await AiWorkerProtocol.ReadAsync<AiWorkerEvent>(stream, CancellationToken.None);
        Assert.Equal(expected, decoded!.Repetition);
        Assert.Null(decoded.Text);
    }

    [Fact]
    public async Task CrashBeforeReply_Retunes_WhilePartialReplyIsNotRepeated()
    {
        bool fail = true;
        using var runtime = Create((request, text, _) =>
        {
            if(request.Command == "generate")
            {
                if(fail) { fail = false; throw new AiWorkerException("0xC0000409"); }
                text("ready");
            }
            return Task.CompletedTask;
        });
        var answer = new StringBuilder();
        await Generate(runtime, Settings, text => answer.Append(text));
        Assert.Equal("ready", answer.ToString());
        Assert.Equal(2, requests.Count(request => request.Command == "generate"));
        Assert.False(runtime.RuntimeOptions!.FlashAttention);
        using var partial = Create((request, text, _) =>
        {
            if(request.Command == "generate") { text("partial"); throw new AiWorkerException("0xC0000409"); }
            return Task.CompletedTask;
        });
        int before = requests.Count(request => request.Command == "generate");
        await Assert.ThrowsAsync<AiException>(() => Generate(partial, Settings));
        Assert.Equal(before + 1, requests.Count(request => request.Command == "generate"));
        Assert.Contains(partial.Notices, notice => notice.ResourceKey == "AI.AutoPartialReply");
    }

    [Fact]
    public async Task ContextReduction_CanBeDisabled_AndHistoryIsNotTruncated()
    {
        using var runtime = Create((request, _, _) =>
        {
            if(request.Command == "probe" && request.Options.ContextLength > 1024) throw new AiException("AI.LoadFailed");
            if(request.Command == "generate") throw new AiException("AI.ContextFull");
            return Task.CompletedTask;
        });
        var error = await Assert.ThrowsAsync<AiException>(() => Generate(runtime, Settings));
        Assert.Equal("AI.AutoContextFull", error.ResourceKey);
        Assert.Equal("private request", Assert.Single(requests.Last().Turns).Content);
        Assert.Equal(1024, runtime.RuntimeOptions!.ContextLength);
        var fixedSettings = Settings with { AutoReduceContext = false };
        Assert.All(AiRuntimeCandidates.Create(fixedSettings, Inspection), candidate => Assert.Equal(4096, candidate.ContextLength));
    }

    [Fact]
    public async Task CacheWriteFailure_DoesNotDiscardWorkingModel()
    {
        using var runtime = Create(store: new AiRuntimeProfileStore(directory));
        await Generate(runtime, Settings);
        Assert.True(runtime.IsLoaded);
        Assert.Contains(runtime.Notices, notice => notice.ResourceKey == "AI.AutoCacheUnavailable");
    }

    [Fact]
    public async Task Unload_CancelsActiveGeneration_AndResetRemovesOnlySelectedModelProfiles()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var runtime = Create(async (request, _, token) =>
        {
            if(request.Command == "generate") { started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
        });
        var generating = Generate(runtime, Settings);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await runtime.UnloadAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generating);
        Assert.False(runtime.IsLoaded);
        var other = new AiRuntimeProfile("other", "other.gguf", new(4096, 0, CpuBackend: true), DateTime.UtcNow);
        Store.Save(other);
        await runtime.ResetAutoProfileAsync(Settings.ModelPath);
        Assert.Null(Store.Find(AiRuntimeProfileStore.Key(Settings, Inspection.HardwareKey)));
        Assert.Equal(other, Store.Find("other"));
    }

    [Fact]
    public void ProfileIdentity_ChangesWithModelContextDriverAndEngineInput()
    {
        string original = AiRuntimeProfileStore.Key(Settings, "driver1");
        Assert.NotEqual(original, AiRuntimeProfileStore.Key(Settings, "driver2"));
        Assert.NotEqual(original, AiRuntimeProfileStore.Key(Settings with { ContextLength = 2048 }, "driver1"));
        Assert.NotEqual(original, AiRuntimeProfileStore.Key(Settings with { AutoReduceContext = false }, "driver1"));
        File.SetLastWriteTimeUtc(Settings.ModelPath, DateTime.UtcNow.AddMinutes(1));
        Assert.NotEqual(original, AiRuntimeProfileStore.Key(Settings, "driver1"));
    }

    [Fact]
    public void CandidateSelection_PreservesManualModeAndBoundsAutomaticRetries()
    {
        var manual = Settings with { MemoryMode = AiMemoryMode.Manual, GpuLayers = -1, ContextLength = 8192 };
        var manualInspection = Inspection with { Plan = Inspection.Plan with { GpuLayers = -1 } };
        var exact = Assert.Single(AiRuntimeCandidates.Create(manual, manualInspection));
        Assert.Equal(-1, exact.GpuLayers);
        Assert.Equal(8192, exact.ContextLength);
        var automatic = AiRuntimeCandidates.Create(Settings with { ContextLength = 131072 }, Inspection);
        Assert.InRange(automatic.Count, 1, 13);
        Assert.Equal(automatic.Count, automatic.Distinct().Count());
        Assert.All(automatic, candidate => Assert.True(candidate.IsValid));
        Assert.Contains(automatic, candidate => candidate.GpuLayers == 0 && candidate.CpuBackend && candidate.ContextLength == 512);
    }

    [Fact]
    public void CorruptOrInvalidCache_IsIgnored_AndOversizedHistoryIsRejectedWithoutTrimming()
    {
        File.WriteAllText(Path.Combine(directory, "profiles.json"), "[null,{\"Key\":\"broken\",\"Options\":null}]");
        Assert.Null(Store.Find("broken"));
        var turns = new[] { new AiTurn("user", new string('a', 23 * 1024 * 1024)) };
        var error = Assert.Throws<AiException>(() => AiWorkerProtocol.ValidateConversation(Settings, turns));
        Assert.Equal("AI.TransferTooLarge", error.ResourceKey);
        Assert.Equal(23 * 1024 * 1024, turns[0].Content.Length);
    }

    [Fact]
    public async Task PipeProtocol_RoundTripsSnapshotsWithoutWpfPreviews_AndRejectsInvalidFrames()
    {
        var turn = new AiTurn("user", "private", [new("image.png", "", new byte[] { 1, 2, 3 }, null, 3)]);
        using var stream = new MemoryStream();
        await AiWorkerProtocol.WriteAsync(stream, AiWorkerTurn.From(turn), CancellationToken.None);
        stream.Position = 0;
        var decoded = (await AiWorkerProtocol.ReadAsync<AiWorkerTurn>(stream, CancellationToken.None))!.ToTurn();
        Assert.Equal(turn.Content, decoded.Content);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(decoded.Attachments!).ImageData.ToArray());
        Assert.Null(decoded.Attachments![0].Preview);
        using var invalid = new MemoryStream(BitConverter.GetBytes(int.MaxValue));
        await Assert.ThrowsAsync<IOException>(async () => await AiWorkerProtocol.ReadAsync<AiWorkerEvent>(invalid, CancellationToken.None));
    }

    public void Dispose() => Directory.Delete(directory, true);

    [Fact]
    public async Task TokenUsage_IsForwardedOnlyForGeneration_AndRoundTripsThroughPipe()
    {
        var expected = new AiTokenUsage(2048, 1200, 37);
        AiTokenUsage? received = null;
        using var runtime = Create();
        runtime.TokenUsageChanged += usage => received = usage;
        await Generate(runtime, Settings);
        Assert.Equal(expected, received);
        using var stream = new MemoryStream();
        await AiWorkerProtocol.WriteAsync(stream, new AiWorkerEvent("usage", Usage: expected), CancellationToken.None);
        stream.Position = 0;
        Assert.Equal(expected, (await AiWorkerProtocol.ReadAsync<AiWorkerEvent>(stream, CancellationToken.None))!.Usage);
    }

    private sealed class FakeWorker(Func<AiWorkerRequest, Action<string>, Action<string>?, CancellationToken, Task<AiModelInspection?>> execute) : IAiWorkerSession
    {
        public bool IsAlive { get; private set; } = true;
        public Task<AiModelInspection?> ExecuteAsync(AiWorkerRequest request, Action<string> onText, IProgress<float>? progress, CancellationToken token, Action<string>? onReasoning = null, Action<AiTokenUsage>? onUsage = null, Action<AiPerformanceMeasurement>? onMeasurement = null)
        {
            if(request.Command == "generate") onUsage?.Invoke(new(2048, 1200, 37));
            return execute(request, onText, onReasoning, token);
        }
        public void Stop() => IsAlive = false;
        public void Dispose() => Stop();
    }
}
