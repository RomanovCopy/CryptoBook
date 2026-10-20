using System.IO;
using System.Text.Json.Serialization;

namespace CryptoBook.AI;

public enum AiMemoryMode { Automatic, CpuOnly, Manual }

public sealed record AiModelSettings
{
    public string ModelPath { get; init; } = string.Empty;
    public int ContextLength { get; init; } = 4096;
    public int MaxTokens { get; init; } = 512;
    public float Temperature { get; init; } = 0.3f;
    public int GpuLayers { get; init; } = 20;
    public AiMemoryMode MemoryMode { get; init; } = AiMemoryMode.Automatic;
    public bool AutoReduceContext { get; init; } = true;
    public bool UseReasoning { get; init; }
    public bool RepetitionProtection { get; init; } = true;
    public string SystemPrompt { get; init; } = string.Empty;

    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(ModelPath) || !File.Exists(ModelPath))
            throw new AiException("AI.ModelMissing");
        if(!ModelPath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
           Path.GetFileName(ModelPath).Contains("mmproj", StringComparison.OrdinalIgnoreCase))
            throw new AiException("AI.InvalidModel");
        using var file = File.OpenRead(ModelPath);
        Span<byte> header = stackalloc byte[4];
        if(file.Read(header) != 4 || !header.SequenceEqual("GGUF"u8))
            throw new AiException("AI.InvalidModel");
        if(ContextLength is < 512 or > 131072) throw new AiException("AI.InvalidContextLength");
        if(MaxTokens is < 1 or > 131072) throw new AiException("AI.InvalidMaxTokens");
        if(!float.IsFinite(Temperature) || Temperature is < 0 or > 2) throw new AiException("AI.InvalidTemperature");
        if(GpuLayers is < -1 or > 999) throw new AiException("AI.InvalidGpuLayers");
        if(!Enum.IsDefined(MemoryMode)) throw new AiException("AI.InvalidMemoryMode");
    }

    public int OutputTokenLimit(int inputTokens)
    {
        // Reserve one token for the end of the turn. MaxTokens is a ceiling, not a reservation.
        int remaining = ContextLength - inputTokens - 1;
        if(remaining < 1) throw new AiException("AI.ContextFull");
        return Math.Min(MaxTokens, remaining);
    }
}

public sealed record AiConfiguration
{
    public bool Enabled { get; init; }
    public string SelectedModelPath { get; init; } = string.Empty;
    public int IdleMinutes { get; init; } = 10;
    public List<AiModelSettings> Models { get; init; } = [];
    public AiChatAppearance ChatAppearance { get; init; } = new();
    [JsonIgnore]
    public AiModelSettings SelectedModel => Models.FirstOrDefault(m =>
        string.Equals(m.ModelPath, SelectedModelPath, StringComparison.OrdinalIgnoreCase))
        ?? new AiModelSettings { ModelPath = SelectedModelPath };
}

public sealed record AiTurn(string Role, string Content, IReadOnlyList<AiAttachment>? Attachments = null);

public class AiException(string resourceKey, Exception? inner = null)
    : Exception(resourceKey, inner)
{
    public string ResourceKey { get; } = resourceKey;
}

public interface IAiInferenceService : IDisposable
{
    bool IsLoaded { get; }
    bool IsBusy { get; }
    DateTime LastUsedUtc { get; }
    Task GenerateAsync(AiModelSettings settings, IReadOnlyList<AiTurn> conversation,
        Action<string> onText, IProgress<float>? loadProgress, CancellationToken cancellationToken, Action<string>? onReasoning = null);
    Task UnloadAsync(CancellationToken cancellationToken = default);
}
