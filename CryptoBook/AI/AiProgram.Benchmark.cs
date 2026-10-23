using System.Diagnostics;

namespace CryptoBook.AI;

internal static partial class AiProgram
{
    private static async Task<AiPerformanceMeasurement> BenchmarkAsync(LocalAiInferenceService engine,
        AiModelSettings settings, AiRuntimeOptions options)
    {
        var test = settings with { ContextLength = options.ContextLength, MemoryMode = AiMemoryMode.Manual,
            GpuLayers = options.GpuLayers, MaxTokens = 64, Temperature = 0, SystemPrompt = "",
            UseReasoning = false, RepetitionProtection = false };
        // Warm up weights/kernels. No user prompt, document or attachment is used or persisted.
        await engine.GenerateAsync(test with { MaxTokens = 8 }, [new("user", "Say OK.")], _ => { }, null,
            CancellationToken.None).ConfigureAwait(false);
        const string prompt = "Write a long, detailed explanation of how a public library works. " +
            "Describe choosing books, borrowing, returning, catalogues, reading rooms and staff. " +
            "Continue for at least ten paragraphs, using complete sentences and concrete examples.";
        var measurements = new List<AiGenerationTiming>();
        for(int run = 0; run < 2; run++)
        {
            await engine.GenerateAsync(test, [new("user", prompt)], _ => { }, null, CancellationToken.None).ConfigureAwait(false);
            if(engine.LastTiming is not { OutputTokens: >= 16 } timing)
                throw new AiException("AI.PerformanceTooShort");
            measurements.Add(timing);
        }
        using var process = Process.GetCurrentProcess();
        return new(measurements.Sum(item => item.InputTokens), measurements.Sum(item => item.OutputTokens) - 1,
            measurements.Sum(item => item.PromptSeconds), measurements.Sum(item => item.GenerationSeconds), process.PeakWorkingSet64);
    }
}
