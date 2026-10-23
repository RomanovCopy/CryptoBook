using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CryptoBook.AI;

public sealed record AiRuntimeOptions(int ContextLength, int GpuLayers, uint BatchSize = 256,
    uint UBatchSize = 128, bool? FlashAttention = null, bool CpuBackend = false,
    int Threads = 0, int BatchThreads = 0, bool PerformanceBoost = false)
{
    internal bool IsValid => ContextLength is >= 512 and <= 131072 && GpuLayers is >= -1 and <= 999 &&
        BatchSize is >= 32 and <= 256 && UBatchSize is >= 32 and <= 128 && UBatchSize <= BatchSize &&
        Threads is >= 0 and <= 1024 && BatchThreads is >= 0 and <= 1024 && (!CpuBackend || GpuLayers == 0);
}

internal sealed record AiModelInspection(AiMemoryPlan Plan, Dictionary<string, string> Metadata, string HardwareKey);
internal sealed record AiRuntimeProfile(string Key, string ModelPath, AiRuntimeOptions Options, DateTime VerifiedUtc,
    AiPerformanceResult? Performance = null);

/// <summary>Only verified runtime parameters. Prompts, replies and attachments never enter this store.</summary>
public sealed class AiRuntimeProfileStore
{
    private readonly string path;
    private readonly object sync = new();
    public AiRuntimeProfileStore() : this(Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "CryptoBook", "ai-runtime-profiles.json")) { }
    public AiRuntimeProfileStore(string path) => this.path = path;

    internal static string Key(AiModelSettings settings, string hardwareKey)
    {
        var file = new FileInfo(settings.ModelPath);
        string identity = string.Join('|', Path.GetFullPath(settings.ModelPath).ToUpperInvariant(), file.Length,
            file.LastWriteTimeUtc.Ticks, settings.ContextLength, settings.AutoReduceContext, hardwareKey,
            typeof(LocalAiInferenceService).Assembly.ManifestModule.ModuleVersionId,
            typeof(LLama.LLamaWeights).Assembly.ManifestModule.ModuleVersionId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    internal AiRuntimeProfile? Find(string key)
    {
        lock(sync) return Read().FirstOrDefault(profile => profile.Key == key && profile.Options is { IsValid: true });
    }
    internal void Save(AiRuntimeProfile profile)
    {
        lock(sync)
        {
            var profiles = Read().Where(item => item.Key != profile.Key).TakeLast(63).ToList();
            profiles.Add(profile);
            Write(profiles);
        }
    }
    internal AiPerformanceResult? LastPerformance(AiModelSettings settings)
    {
        lock(sync) return Read().Where(profile => profile.Options.ContextLength == settings.ContextLength &&
            profile.Key.EndsWith($"|performance|{settings.MemoryMode}|{settings.GpuLayers}", StringComparison.Ordinal) &&
            string.Equals(profile.ModelPath, settings.ModelPath, StringComparison.OrdinalIgnoreCase) &&
            profile.Performance is { IsValid: true }).OrderByDescending(profile => profile.VerifiedUtc).FirstOrDefault()?.Performance;
    }
    internal void Remove(string modelPath)
    {
        lock(sync) Write(Read().Where(item => !string.Equals(item.ModelPath, modelPath, StringComparison.OrdinalIgnoreCase)).ToList());
    }
    private List<AiRuntimeProfile> Read()
    {
        if(!File.Exists(path)) return [];
        // A corrupt cache must not prevent a fresh isolated check.
        try { return (JsonSerializer.Deserialize<List<AiRuntimeProfile>>(File.ReadAllText(path)) ?? [])
                .Where(profile => profile is not null && profile.Options is { IsValid: true } && !string.IsNullOrEmpty(profile.Key))
                .Select(profile => profile.Performance is not null && !profile.Performance.IsValid ? profile with { Performance = null } : profile).ToList(); }
        catch(JsonException) { return []; }
    }
    private void Write(List<AiRuntimeProfile> profiles)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(profiles));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal static class AiRuntimeCandidates
{
    internal static IReadOnlyList<AiRuntimeOptions> Create(AiModelSettings settings, AiModelInspection inspection)
    {
        int gpu = inspection.Plan.GpuLayers;
        // ModelParams itself queries native devices. Keep even construction inside the worker.
        bool? flash = LocalAiInferenceService.NeedsNonFlashAttention(gpu, inspection.Metadata, inspection.Plan.TotalLayers) ? false : null;
        var initial = new AiRuntimeOptions(settings.ContextLength, gpu, FlashAttention: flash, CpuBackend: gpu == 0);
        if(settings.MemoryMode != AiMemoryMode.Automatic) return [initial];
        var candidates = new List<AiRuntimeOptions> { initial };
        if(gpu > 0)
        {
            candidates.Add(initial with { FlashAttention = false });
            candidates.Add(initial with { FlashAttention = false, BatchSize = 128, UBatchSize = 32 });
            candidates.Add(candidates[^1] with { GpuLayers = gpu / 2, CpuBackend = gpu / 2 == 0 });
        }
        var cpu = initial with { GpuLayers = 0, FlashAttention = false, CpuBackend = true, BatchSize = 128, UBatchSize = 32 };
        candidates.Add(cpu);
        if(settings.AutoReduceContext)
            foreach(int context in new[] { 4096, 2048, 1024, 512 }.Where(value => value < settings.ContextLength))
            {
                if(gpu > 0) candidates.Add(cpu with { ContextLength = context, GpuLayers = Math.Max(1, gpu / 2), CpuBackend = false });
                candidates.Add(cpu with { ContextLength = context });
            }
        return candidates.Distinct().ToArray();
    }
}
