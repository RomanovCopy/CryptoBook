using System.Text;

namespace CryptoBook.AI;

public sealed record AiTokenUsage(int ContextLength, int InputTokens, int OutputTokens)
{
    public int RemainingTokens => Math.Max(0, ContextLength - InputTokens - OutputTokens - 1);
}

public interface IAiTokenUsageSource
{
    event Action<AiTokenUsage>? TokenUsageChanged;
}

internal static class AiContextBudget
{
    // An estimate until the worker applies the model's tokenizer and image encoder.
    // UTF-8 bytes also account for Cyrillic, CJK and emoji better than character count / 4.
    internal static int Estimate(AiModelSettings settings, IEnumerable<AiTurn> turns)
    {
        long tokens = 16 + TextTokens(settings.SystemPrompt);
        foreach(var turn in turns)
        {
            tokens += 8 + TextTokens(turn.Content);
            foreach(var file in turn.Attachments ?? [])
                tokens += 12 + TextTokens(file.Name) + TextTokens(file.Text);
        }
        return (int)Math.Min(int.MaxValue, tokens);
    }

    private static long TextTokens(string value) => ((long)Encoding.UTF8.GetByteCount(value) + 2) / 3;
}
