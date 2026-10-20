namespace CryptoBook.AI;

// Only offsets cross the worker pipe; neither exceptions nor runtime notices retain answer text.
internal sealed record AiRepetitionStop(bool IsReasoning, int KeepCharacters);

internal sealed class AiRepetitionException(AiRepetitionStop stop) : AiException("AI.RepetitionStopped")
{
    internal AiRepetitionStop Stop { get; } = stop;
}

/// <summary>Detects sustained, consecutive text loops with bounded memory and exact verification.</summary>
internal sealed class AiRepetitionGuard
{
    private const int MaxPeriod = 2048, Window = MaxPeriod * 4, Capacity = Window + 1;
    private const ulong HashBase = 1099511628211;
    private readonly char[] characters = new char[Capacity];
    private readonly int[] originalEnds = new int[Capacity], letters = new int[Capacity];
    private readonly ulong[] hashes = new ulong[Capacity], powers = new ulong[MaxPeriod + 1];
    private int count;
    private bool previousWhitespace;
    internal int TotalCharacters { get; private set; }

    internal AiRepetitionGuard()
    {
        powers[0] = 1;
        for(int i = 1; i < powers.Length; i++) powers[i] = unchecked(powers[i - 1] * HashBase);
    }

    internal int? Append(string fragment)
    {
        foreach(char character in fragment)
        {
            TotalCharacters++;
            bool whitespace = char.IsWhiteSpace(character);
            if(whitespace && previousWhitespace) continue;
            previousWhitespace = whitespace;
            char normalized = whitespace ? ' ' : character;
            characters[count % Capacity] = normalized;
            originalEnds[count % Capacity] = TotalCharacters;
            int next = (count + 1) % Capacity;
            hashes[next] = unchecked(hashes[count % Capacity] * HashBase + normalized + 1UL);
            letters[next] = letters[count % Capacity] + (char.IsLetter(character) ? 1 : 0);
            count++;
            // Independent of fragment boundaries. A rolling hash only finds candidates;
            // exact comparisons below prevent a collision from stopping a valid answer.
            if(count % 16 == 0 && FindLoop() is int keep) return keep;
        }
        return null;
    }

    internal int? FindLoop()
    {
        int available = Math.Min(count, Window);
        for(int period = 1; period <= Math.Min(MaxPeriod, available / 3); period++)
        {
            // Short phrases need many repeats. Longer passages need at least three.
            // Whitespace, Markdown separators and numeric table cells alone never trigger.
            int copies = period < 48 ? Math.Max(8, (128 + period - 1) / period)
                : Math.Max(3, (192 + period - 1) / period);
            int length = copies * period;
            if(length > available) continue;
            int start = count - length;
            if(letters[count % Capacity] - letters[start % Capacity] < 16) continue;
            ulong last = Hash(count - period, period);
            if(Hash(count - period * 2, period) != last) continue;
            bool matches = true;
            for(int copy = 3; copy <= copies; copy++)
                if(Hash(count - period * copy, period) != last) { matches = false; break; }
            if(!matches) continue;
            for(int i = start + period; i < count; i++)
                if(characters[i % Capacity] != characters[(i - period) % Capacity]) { matches = false; break; }
            if(!matches) continue;
            // Keep the first occurrence and the useful prefix, even if detection happens
            // partway through a subsequent occurrence or after the ring has wrapped.
            int oldest = count - available;
            while(start > oldest && characters[(start - 1) % Capacity] == characters[(start - 1 + period) % Capacity]) start--;
            // A shared full stop/newline at the end of the useful prefix must not rotate
            // the retained occurrence and remove the first passage's ending punctuation.
            while(start < count - period && !char.IsLetterOrDigit(characters[start % Capacity])) start++;
            return originalEnds[(start + period - 1) % Capacity];
        }
        return null;
    }

    private ulong Hash(int start, int length) => unchecked(hashes[(start + length) % Capacity]
        - hashes[start % Capacity] * powers[length]);
}

/// <summary>Shared by text and vision; guards reasoning even when nobody displays it.</summary>
internal static class AiResponseStream
{
    internal static async Task ReadAsync(IAsyncEnumerable<string> generation, Action<string> onText,
        Action<string>? onReasoning, bool startsInReasoning, bool protect, CancellationToken token)
    {
        AiRepetitionGuard? answer = protect ? new() : null, reasoning = protect ? new() : null;
        void Emit(string text, bool isReasoning)
        {
            var guard = isReasoning ? reasoning : answer;
            var callback = isReasoning ? onReasoning : onText;
            int before = guard?.TotalCharacters ?? 0;
            if(guard?.Append(text) is int keep)
            {
                // A large fragment may contain the entire loop. Only forward its useful
                // prefix; the UI also trims duplicates that were streamed earlier.
                int take = Math.Clamp(keep - before, 0, text.Length);
                if(take > 0) callback?.Invoke(text[..take]);
                throw new AiRepetitionException(new(isReasoning, keep));
            }
            callback?.Invoke(text);
        }
        var filter = new AiThinkingFilter(text => Emit(text, true), startsInReasoning);
        await foreach(string fragment in generation.WithCancellation(token).ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();
            filter.Append(fragment, text => Emit(text, false));
        }
        token.ThrowIfCancellationRequested();
        string remaining = filter.Flush();
        if(remaining.Length > 0) Emit(remaining, false);
        if(answer?.FindLoop() is int answerKeep) throw new AiRepetitionException(new(false, answerKeep));
        if(reasoning?.FindLoop() is int reasoningKeep) throw new AiRepetitionException(new(true, reasoningKeep));
    }
}
