using LLama.Native;

namespace CryptoBook.AI;

internal static class AiReplyStartSampler
{
    /// <summary>Require one token, then leave the original end-of-generation scores intact.</summary>
    internal static LLamaToken Sample(Span<float> logits, IReadOnlyList<int> endTokens, Func<LLamaToken> sample)
    {
        var original = new float[endTokens.Count];
        for(int i = 0; i < endTokens.Count; i++)
        {
            original[i] = logits[endTokens[i]];
            logits[endTokens[i]] = float.NegativeInfinity;
        }
        try { return sample(); }
        finally
        {
            for(int i = 0; i < endTokens.Count; i++) logits[endTokens[i]] = original[i];
        }
    }
}
