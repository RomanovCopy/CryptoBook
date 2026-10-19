using CryptoBook.AI;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

internal static class ReasoningProbe
{
    public static async Task RunAsync(string model, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        NativeLibraryConfig.All.WithCuda(false).WithVulkan(true).WithLogCallback((_, _) => { });
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        var settings = new AiModelSettings { ModelPath = model, ContextLength = 2048, MaxTokens = 384, Temperature = 0 };
        var parameters = new ModelParams(model) { ContextSize = 2048, BatchSize = 256, UBatchSize = 128, LoadMode = LLamaLoadMode.MemoryMap,
            Threads = Math.Max(1, Environment.ProcessorCount / 2), BatchThreads = Math.Max(1, Environment.ProcessorCount / 2) };
        AiMemoryPlan plan;
        using(var vocabulary = await LLamaWeights.LoadFromFileAsync(parameters with { VocabOnly = true, GpuLayerCount = 0 }, timeout.Token))
            plan = AiMemoryPlanner.Create(settings, vocabulary.Metadata);
        parameters.GpuLayerCount = Math.Min(8, plan.GpuLayers);
        parameters.NoKqvOffload = plan.GpuLayers == 0;
        parameters.OpOffload = plan.GpuLayers != 0;
        using var weights = await LLamaWeights.LoadFromFileAsync(parameters, timeout.Token);
        parameters = LocalAiInferenceService.ApplyContextCompatibility(parameters, weights.Metadata, plan.TotalLayers);
        weights.Metadata.TryGetValue("tokenizer.chat_template", out string? template);
        File.WriteAllText(Path.Combine(outputDirectory, "model-chat-template.jinja"), template ?? "");
        var results = new List<object>();
        string userTurn = "<|turn>user\nСколько будет 17 + 25? Ответь только числом.<turn|>\n<|turn>model\n";
        foreach(var (name, prompt) in new[] {
            ("old_empty_thought_prefix", userTurn + "<|channel>thought\n<channel|>"),
            ("edge_thinking_disabled", userTurn),
            ("edge_thinking_enabled", "<|turn>system\n<|think|>\n<turn|>\n<|turn>user\nHow many positive divisors does 360 have? Reply with only the count.<turn|>\n<|turn>model\n") })
        {
            var watch = Stopwatch.StartNew();
            var executor = new StatelessExecutor(weights, parameters);
            using var context = executor.Context;
            using var sampling = new DefaultSamplingPipeline { Temperature = 0 };
            var raw = new StringBuilder();
            var visible = new StringBuilder();
            var reasoning = new StringBuilder();
            var filter = new AiThinkingFilter(text => reasoning.Append(text), AiThinkingFilter.StartsInReasoning(prompt));
            await foreach(string fragment in executor.InferAsync(prompt,
                new InferenceParams { MaxTokens = 384, SamplingPipeline = sampling, DecodeSpecialTokens = true }, timeout.Token))
            {
                raw.Append(fragment);
                visible.Append(filter.Append(fragment));
            }
            visible.Append(filter.Flush());
            results.Add(new { name, seconds = watch.Elapsed.TotalSeconds, raw = raw.ToString(), reasoning = reasoning.ToString(), visible = visible.ToString() });
            Console.WriteLine(name + ": " + visible);
        }
        File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(new { plan, gpu_layers_used = parameters.GpuLayerCount, results },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
