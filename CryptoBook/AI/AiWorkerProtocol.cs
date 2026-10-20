using System.IO;
using System.Text.Json;

namespace CryptoBook.AI;

// Pipe-only DTOs intentionally exclude WPF previews. No request data is put on the command line or disk.
internal sealed record AiWorkerAttachment(string Name, string Text, ReadOnlyMemory<byte> Image, long Size);
internal sealed record AiWorkerTurn(string Role, string Content, AiWorkerAttachment[] Attachments)
{
    internal static AiWorkerTurn From(AiTurn turn) => new(turn.Role, turn.Content,
        (turn.Attachments ?? []).Select(file => new AiWorkerAttachment(file.Name, file.Text, file.ImageData, file.Size)).ToArray());
    internal AiTurn ToTurn() => new(Role, Content, Attachments.Select(file =>
        new AiAttachment(file.Name, file.Text, file.Image, null, file.Size)).ToArray());
}
internal sealed record AiWorkerRequest(string Command, AiModelSettings Settings, AiRuntimeOptions Options, AiWorkerTurn[] Turns);
internal sealed record AiWorkerEvent(string Kind, string? Text = null, float Progress = 0, AiModelInspection? Inspection = null,
    AiTokenUsage? Usage = null, AiRepetitionStop? Repetition = null);

internal interface IAiWorkerSession : IDisposable
{
    bool IsAlive { get; }
    Task<AiModelInspection?> ExecuteAsync(AiWorkerRequest request, Action<string> onText,
        IProgress<float>? progress, CancellationToken cancellationToken, Action<string>? onReasoning = null, Action<AiTokenUsage>? onUsage = null);
    void Stop();
}

internal sealed class AiWorkerException(string reason) : Exception(reason);

internal static class AiWorkerProtocol
{
    internal static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            if(bytes.Length > 192 * 1024 * 1024) throw new AiException("AI.TransferTooLarge");
            await stream.WriteAsync(BitConverter.GetBytes(bytes.Length), cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { Array.Clear(bytes); }
    }
    internal static void ValidateConversation(AiModelSettings settings, IReadOnlyList<AiTurn> conversation)
    {
        // Bound allocations before JSON serialization (including worst-case Unicode escaping).
        long estimate = (long)settings.SystemPrompt.Length * 6;
        if(estimate > 128 * 1024 * 1024) throw new AiException("AI.TransferTooLarge");
        foreach(var turn in conversation)
        {
            estimate += (long)turn.Content.Length * 6 + 256;
            foreach(var file in turn.Attachments ?? [])
                estimate += (long)file.Text.Length * 6 + (long)file.ImageData.Length * 4 / 3 + 1024;
            if(estimate > 128 * 1024 * 1024) throw new AiException("AI.TransferTooLarge");
        }
    }
    internal static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[4];
        int read = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
        if(read == 0) return default;
        await stream.ReadExactlyAsync(header.AsMemory(read), cancellationToken).ConfigureAwait(false);
        int length = BitConverter.ToInt32(header);
        if(length is < 1 or > 192 * 1024 * 1024) throw new IOException("Invalid AI worker frame.");
        byte[] bytes = new byte[length];
        try
        {
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(bytes);
        }
        finally { Array.Clear(bytes); }
    }
}
