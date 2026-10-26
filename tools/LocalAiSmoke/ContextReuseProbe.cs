using CryptoBook.AI;
using LLama.Native;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

internal static class ContextReuseProbe
{
    internal static async Task RunAsync(string modelPath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var token = deadline.Token;
        var settings = new AiModelSettings
        {
            ModelPath = Path.GetFullPath(modelPath), ContextLength = 2048, MaxTokens = 48,
            Temperature = 0, ReuseContext = true, PreloadOnPanelOpen = true, RepetitionProtection = false
        };
        // Exercise the actual pipe commands. All inputs are synthetic; user settings are untouched.
        using(var adaptive = new AdaptiveAiInferenceService(new(Path.Combine(outputDirectory, "profiles.json"))))
        {
            await adaptive.LoadAsync(settings, null, token);
            Check(adaptive.IsLoaded, "Isolated preload did not keep the model loaded.");
            await adaptive.ResetContextAsync(token);
            Check(adaptive.IsLoaded, "Reset unloaded the isolated model.");
            var text = new StringBuilder();
            await adaptive.GenerateAsync(settings, [new("user", "Reply with one word: ready")], part => text.Append(part), null, token);
            Check(text.ToString().Trim().Length > 0, "Isolated generation after reset was empty.");
            await adaptive.UnloadAsync(token);
            Check(!adaptive.IsLoaded, "Isolated unload left the model loaded.");
        }
        NativeLibraryConfig.All.WithCuda(false).WithVulkan().WithAutoFallback().WithLogCallback((_, _) => { });
        using var engine = new LocalAiInferenceService();
        var runs = new List<object>();
        async Task<string> Generate(string name, AiModelSettings profile, IReadOnlyList<AiTurn> turns)
        {
            var response = new StringBuilder();
            var watch = Stopwatch.StartNew();
            await engine.GenerateAsync(profile, turns, part => response.Append(part), null, token);
            runs.Add(new { name, reused_tokens = engine.LastReusedTokens, elapsed_ms = watch.ElapsedMilliseconds,
                timing = engine.LastTiming, answer = response.ToString() });
            return response.ToString();
        }
        await engine.LoadAsync(settings, null, token);
        Check(engine.IsLoaded && engine.LastReusedTokens == 0, "Preload must not invent a conversation.");
        AiTurn question = new("user", "Remember this code: ORBIT-731. Reply only with OK.");
        string first = await Generate("first", settings, [question]);
        Check(engine.LastReusedTokens == 0, "First request reused another conversation.");
        AiTurn followup = new("user", "What code did I ask you to remember? Reply only with that code.");
        AiTurn[] history = [question, new("assistant", first), followup];
        string cached = await Generate("followup_cached", settings, history);
        int reused = engine.LastReusedTokens;
        Check(reused > 0, "The unchanged text history was not reused.");
        Check(cached.Contains("ORBIT-731", StringComparison.Ordinal), "Cached reply lost the conversation.");
        string uncached = await Generate("followup_uncached", settings with { ReuseContext = false }, history);
        Check(engine.LastReusedTokens == 0, "Opting out retained a computation cache.");
        Check(uncached.Contains("ORBIT-731", StringComparison.Ordinal), "Baseline reply lost the conversation.");
        await Generate("seed_again", settings, [question]);
        var edited = question with { Content = question.Content.Replace("ORBIT-731", "DELTA-942", StringComparison.Ordinal) };
        string changed = await Generate("edited_history", settings, [edited, new("assistant", first), followup]);
        Check(changed.Contains("DELTA-942", StringComparison.Ordinal) && !changed.Contains("ORBIT-731", StringComparison.Ordinal),
            "A changed prefix reused stale data.");
        await engine.ResetContextAsync(token);
        Check(engine.IsLoaded && engine.LastReusedTokens == 0, "Chat reset must release context and keep weights.");
        await Generate("new_chat", settings, [new("user", "Reply with one word: ready")]);
        Check(engine.LastReusedTokens == 0, "New chat reused old tokens.");
        using(var stop = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            bool cancelled = false;
            try
            {
                await engine.GenerateAsync(settings with { MaxTokens = 128 }, [new("user", "Explain how a library works in ten paragraphs.")],
                    _ => stop.Cancel(), null, stop.Token);
            }
            catch(OperationCanceledException) { cancelled = true; }
            Check(cancelled && engine.LastReusedTokens == 0, "Cancellation retained partial native state.");
        }
        await Generate("after_cancel", settings, [new("user", "Reply with one word: ready")]);
        Check(engine.LastReusedTokens == 0, "Request after cancellation reused partial state.");
        await engine.UnloadAsync(token);
        Check(!engine.IsLoaded && engine.LastReusedTokens == 0, "Unload retained the model or context.");
        File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(new
        {
            passed = true, native_model = true, isolated_load_reset_generate_unload = true,
            prefix_reused_tokens = reused, cached_and_uncached_answers_equal = cached == uncached,
            edited_history = true, opt_out = true, chat_reset = true, cancellation = true, unload = true, runs
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Native context reuse and preload passed: " + outputDirectory);
    }

    private static void Check(bool value, string error) { if(!value) throw new Exception(error); }
}
