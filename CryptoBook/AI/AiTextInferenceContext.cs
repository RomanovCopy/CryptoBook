using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using System.Runtime.CompilerServices;

namespace CryptoBook.AI;

/// <summary>In-memory token prefix and native state, owned by one isolated worker.</summary>
internal sealed class AiTextInferenceContext(LLamaContext context) : IDisposable
{
    private readonly List<LLamaToken> evaluated = [];
    internal int LastReusedTokens { get; private set; }

    internal static int CommonPrefix(IReadOnlyList<LLamaToken> cached, IReadOnlyList<LLamaToken> prompt)
    {
        // Always evaluate the last input token to obtain current logits, even for an identical request.
        int limit = Math.Min(cached.Count, prompt.Count - 1), count = 0;
        while(count < limit && cached[count] == prompt[count]) count++;
        return count;
    }

    internal async IAsyncEnumerable<string> InferAsync(LLamaToken[] prompt, DefaultSamplingPipeline sampling,
        int maxTokens, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int past = CommonPrefix(evaluated, prompt);
        if(past < evaluated.Count)
        {
            bool evictedPrefix = (int)context.NativeHandle.MemorySequenceMinPosition(LLamaSeqId.Zero) > 0;
            context.NativeHandle.MemorySequenceRemove(LLamaSeqId.Zero, past, -1);
            // Recurrent/hybrid models may refuse partial rollback. Never assume removal succeeded.
            if(evictedPrefix || (int)context.NativeHandle.MemorySequenceMaxPosition(LLamaSeqId.Zero) >= past)
                past = 0;
        }
        if(past == 0) context.NativeHandle.MemoryClear();
        evaluated.RemoveRange(past, evaluated.Count - past);
        LastReusedTokens = past;
        var batch = new LLamaBatch();
        var input = prompt.Skip(past).ToList();
        var (result, _, next) = await context.DecodeAsync(input, LLamaSeqId.Zero, batch, past, cancellationToken).ConfigureAwait(false);
        if(result != DecodeResult.Ok) throw new LLama.Exceptions.LLamaDecodeError(result);
        evaluated.AddRange(input);
        past = next;
        sampling.Reset();
        var decoder = new StreamingTokenDecoder(context) { DecodeSpecialTokens = true };
        for(int index = 0; index < maxTokens; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = sampling.Sample(context.NativeHandle, -1);
            if(token.IsEndOfGeneration(context.Vocab)) yield break;
            decoder.Add(token);
            yield return decoder.Read();
            if(index + 1 == maxTokens) yield break;
            (result, _, next) = await context.DecodeAsync(new List<LLamaToken> { token }, LLamaSeqId.Zero,
                batch, past, cancellationToken).ConfigureAwait(false);
            if(result != DecodeResult.Ok) throw new LLama.Exceptions.LLamaDecodeError(result);
            evaluated.Add(token);
            past = next;
        }
    }

    public void Dispose()
    {
        evaluated.Clear();
        context.Dispose();
    }
}
