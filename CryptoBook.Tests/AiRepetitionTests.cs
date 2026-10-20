using CryptoBook.AI;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CryptoBook.Tests;

public sealed class AiRepetitionTests
{
    private const string Passage = "The document stays on this computer and can be opened again after the next application launch. ";

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(53)]
    [InlineData(20000)]
    public async Task Loop_StopsAndKeepsFirstOccurrence_RegardlessOfFragmentSize(int fragmentSize)
    {
        string prefix = "Useful result: 42.\n";
        string source = prefix + string.Concat(Enumerable.Repeat(Passage, 100));
        var answer = new StringBuilder();
        bool disposed = false;
        int readCharacters = 0;
        async IAsyncEnumerable<string> Generate()
        {
            try
            {
                for(int offset = 0; offset < source.Length; offset += fragmentSize)
                {
                    string fragment = source.Substring(offset, Math.Min(fragmentSize, source.Length - offset));
                    readCharacters += fragment.Length;
                    yield return fragment;
                    await Task.Yield();
                }
            }
            finally { disposed = true; }
        }
        var error = await Assert.ThrowsAsync<AiRepetitionException>(() => AiResponseStream.ReadAsync(Generate(),
            text => answer.Append(text), null, false, true, CancellationToken.None));
        Assert.True(disposed);
        Assert.False(error.Stop.IsReasoning);
        string kept = answer.ToString()[..error.Stop.KeepCharacters];
        Assert.Equal((prefix + Passage).TrimEnd(), kept.TrimEnd());
        if(fragmentSize < source.Length) Assert.True(readCharacters < source.Length);
    }

    [Theory]
    [InlineData("yes ")]
    [InlineData("да ")]
    [InlineData("abc")]
    [InlineData("继续")]
    [InlineData("x")]
    public void ShortWordAndUnbrokenLoops_AreDetected(string repeat)
    {
        var guard = new AiRepetitionGuard();
        Assert.NotNull(guard.Append(string.Concat(Enumerable.Repeat(repeat, 300))));
    }

    [Fact]
    public void ParagraphLoop_NormalizesWhitespaceAndSurvivesWindowWrap()
    {
        var guard = new AiRepetitionGuard();
        string prefix = string.Join('\n', Enumerable.Range(0, 3000).Select(i => $"Unique item {i} has a value of {i * 7}.")) + "\n";
        Assert.Null(guard.Append(prefix));
        string repeats = Passage + Passage.Replace(" ", "\r\n  ") + Passage.Replace(" ", "\t");
        int? keep = guard.Append(repeats) ?? guard.FindLoop();
        Assert.NotNull(keep);
        Assert.Equal((prefix + Passage).TrimEnd(), (prefix + repeats)[..keep.Value].TrimEnd());
    }

    [Fact]
    public async Task NormalCodeTablesTerminologyAndTwoQuotes_KeepTheirExactText()
    {
        string source = "```csharp\n" + string.Join('\n', Enumerable.Range(0, 300).Select(i => $"values[{i}] = ReadValue({i});"))
            + "\n}\n}\n}\n```\n| Name | Value |\n|---|---|\n"
            + string.Join('\n', Enumerable.Range(0, 300).Select(i => $"| document | {i} |"))
            + "\n" + new string('-', 1000) + "\n" + new string('0', 1000)
            + "\nQuotation: " + Passage + "\nAgain: " + Passage;
        var answer = new StringBuilder();
        await AiResponseStream.ReadAsync(Fragments(source, 7), text => answer.Append(text), null,
            false, true, CancellationToken.None);
        Assert.Equal(source, answer.ToString());
    }

    [Theory]
    [InlineData("<think>", "</think>", false)]
    [InlineData("<|channel|>analysis<|message|>", "<|channel|>final<|message|>", false)]
    [InlineData("<|channel>thought\n", "<channel|>", false)]
    [InlineData("", "</think>", true)]
    public async Task ReasoningLoop_StopsEvenWithoutReasoningCallback(string open, string close, bool startsInReasoning)
    {
        var answer = new StringBuilder();
        string source = open + string.Concat(Enumerable.Repeat(Passage, 30)) + close + "unreachable final answer";
        var error = await Assert.ThrowsAsync<AiRepetitionException>(() => AiResponseStream.ReadAsync(Fragments(source, 3),
            text => answer.Append(text), null, startsInReasoning, true, CancellationToken.None));
        Assert.True(error.Stop.IsReasoning);
        Assert.Empty(answer.ToString());
    }

    [Fact]
    public async Task Channels_AreCheckedSeparately_AndUsefulAnswerBeforeReasoningIsKept()
    {
        var answer = new StringBuilder();
        var reasoning = new StringBuilder();
        await AiResponseStream.ReadAsync(Fragments("<think>" + Passage + "</think>" + Passage, 1),
            text => answer.Append(text), text => reasoning.Append(text), false, true, CancellationToken.None);
        Assert.Equal(Passage, answer.ToString());
        Assert.Equal(Passage, reasoning.ToString());

        answer.Clear();
        var error = await Assert.ThrowsAsync<AiRepetitionException>(() => AiResponseStream.ReadAsync(
            Fragments("Useful answer.<think>" + string.Concat(Enumerable.Repeat(Passage, 30)), 20000),
            text => answer.Append(text), null, false, true, CancellationToken.None));
        Assert.True(error.Stop.IsReasoning);
        Assert.Equal("Useful answer.", answer.ToString());
    }

    [Fact]
    public async Task DisabledProtection_AllowsIntentionalRepetition()
    {
        string source = string.Concat(Enumerable.Repeat(Passage, 20));
        var answer = new StringBuilder();
        await AiResponseStream.ReadAsync(Fragments(source, 11), text => answer.Append(text), null,
            false, false, CancellationToken.None);
        Assert.Equal(source, answer.ToString());
        Assert.True(JsonSerializer.Deserialize<AiModelSettings>("{}")!.RepetitionProtection);
    }

    [Fact]
    public async Task Cancellation_DisposesTheStream_AndDoesNotBecomeARepetitionStop()
    {
        using var cancellation = new CancellationTokenSource();
        bool disposed = false;
        async IAsyncEnumerable<string> Generate([EnumeratorCancellation] CancellationToken token = default)
        {
            try { yield return "Useful answer."; await Task.Delay(Timeout.Infinite, token); }
            finally { disposed = true; }
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AiResponseStream.ReadAsync(Generate(),
            _ => cancellation.Cancel(), null, false, true, cancellation.Token));
        Assert.True(disposed);
    }

    private static async IAsyncEnumerable<string> Fragments(string source, int size)
    {
        for(int offset = 0; offset < source.Length; offset += size)
        {
            yield return source.Substring(offset, Math.Min(size, source.Length - offset));
            await Task.Yield();
        }
    }
}
