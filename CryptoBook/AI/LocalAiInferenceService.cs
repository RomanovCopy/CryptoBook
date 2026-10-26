using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using LLama.Transformers;
using System.Text;
using System.Runtime.CompilerServices;

namespace CryptoBook.AI;

/// <summary>Native engine used inside the isolated AI worker. No network or saved chat.</summary>
public sealed class LocalAiInferenceService : IAiInferenceService, IAiTokenUsageSource
{
    internal AiGenerationTiming? LastTiming { get; private set; }
    private readonly AiRuntimeOptions? runtimeOptions;
    public LocalAiInferenceService() { }
    internal LocalAiInferenceService(AiRuntimeOptions options) => runtimeOptions = options;
    private readonly SemaphoreSlim gate = new(1, 1);
    private LLamaWeights? weights;
    private AiTextInferenceContext? textContext;
    internal int LastReusedTokens => textContext?.LastReusedTokens ?? 0;
    private MtmdWeights? projector;
    private string? loadedProjectorPath;
    private string? loadedPath;
    private int loadedLayers;
    private AiMemoryMode loadedMemoryMode;
    private int loadedContextLength;
    public AiMemoryPlan? LastMemoryPlan { get; private set; }
    private bool disposed;
    private CancellationTokenSource? activeCancellation;
    private readonly object cancellationLock = new();
    private long generationEpoch;
    public bool IsLoaded => weights is not null;
    public bool IsBusy { get; private set; }
    public DateTime LastUsedUtc { get; private set; } = DateTime.UtcNow;
    public event Action<AiTokenUsage>? TokenUsageChanged;

    public Task GenerateAsync(AiModelSettings settings, IReadOnlyList<AiTurn> conversation,
        Action<string> onText, IProgress<float>? loadProgress, CancellationToken cancellationToken, Action<string>? onReasoning = null)
        => RunAsync(settings, conversation, onText, loadProgress, cancellationToken, onReasoning);

    public Task LoadAsync(AiModelSettings settings, IProgress<float>? loadProgress, CancellationToken cancellationToken)
        => RunAsync(settings, [], _ => { }, loadProgress, cancellationToken, preload: true);

    private async Task RunAsync(AiModelSettings settings, IReadOnlyList<AiTurn> conversation,
        Action<string> onText, IProgress<float>? loadProgress, CancellationToken cancellationToken,
        Action<string>? onReasoning = null, bool preload = false)
    {
        settings.Validate();
        var images = conversation.SelectMany(turn => turn.Attachments ?? []).Where(file => file.IsImage).ToArray();
        string? projectorPath = images.Length > 0 ? AiVisionProjector.Find(settings.ModelPath) : null;
        long startedEpoch;
        lock(cancellationLock) startedEpoch = generationEpoch;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationToken = requestCancellation.Token;
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            IsBusy = true;
            lock(cancellationLock)
            {
                if(startedEpoch != generationEpoch) throw new OperationCanceledException();
                activeCancellation = requestCancellation;
            }
            var parameters = new ModelParams(settings.ModelPath)
            {
                ContextSize = (uint)settings.ContextLength,
                GpuLayerCount = settings.GpuLayers,
                BatchSize = runtimeOptions?.BatchSize ?? 256,
                UBatchSize = runtimeOptions?.UBatchSize ?? 128,
                FlashAttention = runtimeOptions?.FlashAttention,
                Threads = runtimeOptions?.Threads is > 0 ? runtimeOptions.Threads : Math.Max(1, Environment.ProcessorCount / 2),
                BatchThreads = runtimeOptions?.BatchThreads is > 0 ? runtimeOptions.BatchThreads : Math.Max(1, Environment.ProcessorCount / 2),
                LoadMode = LLamaLoadMode.MemoryMap,
            };
            if(weights is null || loadedPath != settings.ModelPath || loadedLayers != settings.GpuLayers ||
               loadedMemoryMode != settings.MemoryMode || loadedContextLength != settings.ContextLength)
            {
                UnloadCore();
                try
                {
                    using(var vocabulary = await LLamaWeights.LoadFromFileAsync(parameters with
                        { VocabOnly = true, GpuLayerCount = 0 }, cancellationToken).ConfigureAwait(false))
                        LastMemoryPlan = AiMemoryPlanner.Create(settings, vocabulary.Metadata);
                    parameters.GpuLayerCount = LastMemoryPlan.GpuLayers;
                    parameters.NoKqvOffload = parameters.GpuLayerCount == 0;
                    parameters.OpOffload = parameters.GpuLayerCount != 0;
                    for(int attempt = 0; ; attempt++)
                    {
                        try
                        {
                            weights = await LLamaWeights.LoadFromFileAsync(parameters,
                                cancellationToken, loadProgress).ConfigureAwait(false);
                            break;
                        }
                        catch(LLama.Exceptions.LoadWeightsFailedException) when(
                            runtimeOptions is null && settings.MemoryMode == AiMemoryMode.Automatic && parameters.GpuLayerCount > 0 && attempt < 2)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            parameters.GpuLayerCount = attempt == 0 ? parameters.GpuLayerCount / 2 : 0;
                            parameters.NoKqvOffload = parameters.GpuLayerCount == 0;
                            parameters.OpOffload = parameters.GpuLayerCount != 0;
                            double fraction = (double)parameters.GpuLayerCount / (LastMemoryPlan.TotalLayers + 1);
                            LastMemoryPlan = LastMemoryPlan with { GpuLayers = parameters.GpuLayerCount,
                                EstimatedGpuWeightBytes = (long)(LastMemoryPlan.ModelBytes * fraction),
                                EstimatedRamWeightBytes = (long)(LastMemoryPlan.ModelBytes * (1 - fraction)) };
                        }
                    }
                    loadedPath = settings.ModelPath;
                    loadedLayers = settings.GpuLayers;
                    loadedMemoryMode = settings.MemoryMode;
                    loadedContextLength = settings.ContextLength;
                }
                catch(OperationCanceledException) { throw; }
                catch(Exception e) { throw new AiException("AI.LoadFailed", e); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            parameters.GpuLayerCount = LastMemoryPlan?.GpuLayers ?? settings.GpuLayers;
            parameters.NoKqvOffload = parameters.GpuLayerCount == 0;
            parameters.OpOffload = parameters.GpuLayerCount != 0;
            parameters = ApplyContextCompatibility(parameters, weights.Metadata, LastMemoryPlan!.TotalLayers);
            if(!settings.ReuseContext || images.Length > 0) ResetContextCore();
            if(preload)
            {
                if(settings.ReuseContext)
                    await Task.Run(() => textContext ??= new(weights.CreateContext(parameters)), cancellationToken).ConfigureAwait(false);
                loadProgress?.Report(1);
                return;
            }
            if(projectorPath is not null && (projector is null || loadedProjectorPath != projectorPath))
            {
                projector?.Dispose(); projector = null;
                var visionParameters = MtmdContextParams.Default();
                // Keep the vision encoder in RAM so model layers and context can share the GPU budget.
                visionParameters.UseGpu = false;
                visionParameters.Warmup = false;
                visionParameters.NThreads = parameters.Threads ?? Math.Max(1, Environment.ProcessorCount / 2);
                try
                {
                    projector = await MtmdWeights.LoadFromFileAsync(projectorPath, weights, visionParameters, cancellationToken).ConfigureAwait(false);
                    if(!projector.SupportsVision) throw new AiException("AI.ImageNeedsVisionModel");
                    loadedProjectorPath = projectorPath;
                }
                catch(OperationCanceledException) { throw; }
                catch(Exception error) { projector?.Dispose(); projector = null; throw new AiException("AI.ImageLoadFailed", error); }
            }
            loadProgress?.Report(1);
            await Task.Run(async () =>
            {
                string prompt;
                string mediaMarker = images.Length > 0 ? NativeApi.MtmdDefaultMarker() ?? "<media>" : "<media>";
                var promptTurns = images.Length > 0 ? AiChatFormat.WithImagesInLatestTurn(conversation) : conversation;
                weights.Metadata.TryGetValue("general.architecture", out string? architecture);
                if(architecture == "gemma4") prompt = AiChatFormat.Gemma4(settings, promptTurns, mediaMarker, weights.Metadata);
                else
                {
                    var template = new LLamaTemplate(weights) { AddAssistant = true };
                    if(!string.IsNullOrWhiteSpace(settings.SystemPrompt))
                        template.Add("system", settings.SystemPrompt);
                    foreach(AiTurn turn in promptTurns) template.Add(turn.Role, AiChatFormat.Content(turn, mediaMarker));
                    prompt = PromptTemplateTransformer.ToModelPrompt(template);
                }
                // Qwen thinking is controlled by the assistant prefix. Other model templates stay intact.
                if(!settings.UseReasoning && weights.Metadata.TryGetValue("general.architecture", out string? arch) &&
                   arch.StartsWith("qwen", StringComparison.OrdinalIgnoreCase) &&
                   weights.Metadata.TryGetValue("tokenizer.chat_template", out string? chatTemplate) &&
                   chatTemplate.Contains("<think>", StringComparison.Ordinal))
                    prompt = prompt.EndsWith("<think>\n", StringComparison.Ordinal)
                        ? prompt + "\n</think>\n\n" : prompt + "<think>\n\n</think>\n\n";
                var embeds = new List<SafeMtmdEmbed>();
                SafeMtmdInputChunks? imageChunks = null;
                LLamaContext? context = null;
                try
                {
                    int inputTokens;
                    LLamaToken[]? promptTokens = null;
                    if(images.Length > 0)
                    {
                        foreach(var image in images) embeds.Add(projector!.LoadMedia(image.ImageData.Span));
                        int result = projector!.Tokenize(prompt, true, true, embeds.ToArray(), out imageChunks);
                        if(result != 0 || imageChunks is null) throw new AiException("AI.ImageLoadFailed");
                        inputTokens = checked((int)Math.Max(imageChunks.CountTokens(), (ulong)imageChunks.CountPositions()));
                    }
                    else
                    {
                        promptTokens = weights.Tokenize(prompt, add_bos: true, special: true, Encoding.UTF8);
                        inputTokens = promptTokens.Length;
                    }
                    int generatedTokens = 0;
                    long lastUsageUpdate = Environment.TickCount64;
                    void ReportUsage() => TokenUsageChanged?.Invoke(new(settings.ContextLength, inputTokens, generatedTokens));
                    ReportUsage();
                    int outputTokens = settings.OutputTokenLimit(inputTokens);
                    LastTiming = null;
                    long samplingStarted = 0, firstToken = 0, lastToken = 0;
                    using var sampling = new TokenCountingPipeline(settings.RepetitionProtection, token =>
                    {
                        if(token.IsEndOfGeneration(weights.Vocab)) return;
                        lastToken = System.Diagnostics.Stopwatch.GetTimestamp();
                        if(firstToken == 0) firstToken = lastToken;
                        generatedTokens++;
                        if(Environment.TickCount64 - lastUsageUpdate < 100) return;
                        lastUsageUpdate = Environment.TickCount64;
                        ReportUsage();
                    }) { Temperature = settings.Temperature };
                    IAsyncEnumerable<string> generation;
                    if(images.Length > 0)
                    {
                        context = weights.CreateContext(parameters);
                        generation = InferVisionAsync(context, imageChunks!, sampling, outputTokens, cancellationToken);
                    }
                    else if(settings.ReuseContext)
                    {
                        textContext ??= new(weights.CreateContext(parameters));
                        generation = textContext.InferAsync(promptTokens!, sampling, outputTokens, cancellationToken);
                    }
                    else
                    {
                        var executor = new StatelessExecutor(weights, parameters);
                        context = executor.Context;
                        // Keep channel boundaries until AiThinkingFilter has removed hidden reasoning.
                        generation = executor.InferAsync(prompt, new InferenceParams { MaxTokens = outputTokens, DecodeSpecialTokens = true,
                            SamplingPipeline = sampling, OverflowStrategy = ContextOverflowStrategy.ThrowException }, cancellationToken);
                    }
                    try
                    {
                        samplingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                        await AiResponseStream.ReadAsync(generation, onText, onReasoning,
                            AiThinkingFilter.StartsInReasoning(prompt), settings.RepetitionProtection, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        if(firstToken > 0 && samplingStarted > 0)
                            LastTiming = new(inputTokens, generatedTokens,
                                System.Diagnostics.Stopwatch.GetElapsedTime(samplingStarted, firstToken).TotalSeconds,
                                System.Diagnostics.Stopwatch.GetElapsedTime(firstToken, lastToken).TotalSeconds);
                        ReportUsage();
                    }
                }
                finally
                {
                    imageChunks?.Dispose();
                    foreach(var embed in embeds) embed.Dispose();
                    projector?.ClearMedia();
                    context?.Dispose();
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ResetContextCore();
            throw;
        }
        finally
        {
            lock(cancellationLock) activeCancellation = null;
            LastUsedUtc = DateTime.UtcNow;
            IsBusy = false;
            gate.Release();
        }
    }

    private sealed class TokenCountingPipeline(bool protect, Action<LLamaToken> onToken) : DefaultSamplingPipeline
    {
        protected override SafeLLamaSamplerChainHandle CreateChain(SafeLLamaContextHandle context)
        {
            if(!protect) return base.CreateChain(context);
            using var defaults = base.CreateChain(context);
            var chain = SafeLLamaSamplerChainHandle.Create(LLamaSamplerChainParams.Default());
            try
            {
                // Penalize growing sequences before probability filters, preserving the
                // library's remaining stages and avoiding a blanket penalty on terminology.
                chain.AddDry(context.ModelHandle, ["\n", ":", "\"", "*"], 0.5f, 1.75f, 4, 512);
                for(int i = 0; i < defaults.Count; i++) chain.AddClone(defaults, i);
                return chain;
            }
            catch { chain.Dispose(); throw; }
        }

        public override LLamaToken Sample(SafeLLamaContextHandle ctx, int index)
        {
            var token = base.Sample(ctx, index);
            onToken(token);
            return token;
        }
    }

    internal static ModelParams ApplyContextCompatibility(ModelParams parameters,
        IReadOnlyDictionary<string, string> metadata, int totalLayers)
    {
        // Gemma 4 E2B/E4B shares KV between layers. With partial GPU offload, Flash
        // Attention can exceed the bundled ggml scheduler's split-input limit and abort
        // the process (llama.cpp #21730/#22133). Apply before both text and vision contexts,
        // including requests which reuse loaded weights; a managed catch cannot recover.
        if(NeedsNonFlashAttention(parameters.GpuLayerCount, metadata, totalLayers))
            return parameters with { FlashAttention = false };
        return parameters;
    }

    internal static bool NeedsNonFlashAttention(int gpuLayers, IReadOnlyDictionary<string, string> metadata, int totalLayers)
    {
        bool partialOffload = gpuLayers > 0 && gpuLayers <= totalLayers;
        return partialOffload && metadata.TryGetValue("general.architecture", out string? architecture) &&
           architecture == "gemma4" && metadata.TryGetValue("gemma4.embedding_length_per_layer_input", out string? embedding) &&
           int.TryParse(embedding, out int size) && size > 0;
    }

    private async IAsyncEnumerable<string> InferVisionAsync(LLamaContext context, SafeMtmdInputChunks chunks,
        DefaultSamplingPipeline sampling, int maxTokens, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int past = 0;
        int status = projector!.EvaluateChunks(chunks, context.NativeHandle, ref past, 0, checked((int)context.BatchSize), true);
        if(status != 0) throw new AiException("AI.ImageLoadFailed");
        var decoder = new StreamingTokenDecoder(context) { DecodeSpecialTokens = true };
        var batch = new LLamaBatch();
        sampling.Reset();
        for(int i = 0; i < maxTokens; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = i == 0 ? SampleVisionStart(context, sampling) : sampling.Sample(context.NativeHandle, -1);
            if(token.IsEndOfGeneration(weights!.Vocab)) yield break;
            decoder.Add(token);
            yield return decoder.Read();
            if(i + 1 == maxTokens) yield break;
            var (result, _, next) = await context.DecodeAsync(new List<LLamaToken> { token }, LLamaSeqId.Zero,
                batch, past, cancellationToken).ConfigureAwait(false);
            if(result != DecodeResult.Ok) throw new LLama.Exceptions.LLamaDecodeError(result);
            past = next;
        }
    }

    private LLamaToken SampleVisionStart(LLamaContext context, DefaultSamplingPipeline sampling)
    {
        // A reply must contain at least one token. At nonzero temperature, an otherwise valid
        // image answer can randomly select EOG first. Restore normal stop-token handling immediately.
        var logits = context.NativeHandle.GetLogitsIth(-1);
        var endTokens = new List<int>();
        for(int index = 0; index < logits.Length; index++)
        {
            if(!((LLamaToken)index).IsEndOfGeneration(weights!.Vocab)) continue;
            endTokens.Add(index);
        }
        return AiReplyStartSampler.Sample(logits, endTokens, () => sampling.Sample(context.NativeHandle, -1));
    }

    public async Task UnloadAsync(CancellationToken cancellationToken = default)
    {
        lock(cancellationLock) { generationEpoch++; activeCancellation?.Cancel(); }
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { UnloadCore(); }
        finally { gate.Release(); }
    }

    public async Task ResetContextAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { ResetContextCore(); }
        finally { gate.Release(); }
    }

    private void ResetContextCore()
    {
        textContext?.Dispose(); textContext = null;
    }

    private void UnloadCore()
    {
        ResetContextCore();
        projector?.Dispose(); projector = null; loadedProjectorPath = null;
        weights?.Dispose();
        weights = null;
        loadedPath = null;
        LastMemoryPlan = null;
    }

    public void Dispose()
    {
        lock(cancellationLock) { generationEpoch++; activeCancellation?.Cancel(); }
        gate.Wait();
        try { disposed = true; UnloadCore(); }
        finally { gate.Release(); }
    }
}

/// <summary>Separates reasoning from the answer even when tags span streamed fragments.</summary>
public sealed class AiThinkingFilter
{
    private static readonly (string Tag, bool? Thinking)[] Tags =
    [
        ("<think>", true), ("</think>", false),
        ("<|channel|>analysis<|message|>", true), ("<|channel|>final<|message|>", false),
        ("<|start|>assistant", null), ("<|end|>", null), ("<|fim_suffix|>", null),
        ("<|channel>thought\n", true), ("<channel|>", false),
        ("<|turn>model\n", null), ("<turn|>", null)
    ];
    private string pending = string.Empty;
    private bool thinking;
    private readonly Action<string>? onReasoning;
    public AiThinkingFilter(Action<string>? onReasoning = null, bool startsInReasoning = false)
    {
        this.onReasoning = onReasoning;
        thinking = startsInReasoning;
    }
    internal static bool StartsInReasoning(string prompt)
    {
        string suffix = prompt.TrimEnd();
        return suffix.EndsWith("<think>", StringComparison.Ordinal) ||
            suffix.EndsWith("<|channel>thought", StringComparison.Ordinal) ||
            suffix.EndsWith("<|channel|>analysis<|message|>", StringComparison.Ordinal);
    }
    public string Append(string fragment)
    {
        var visible = new StringBuilder();
        Append(fragment, text => visible.Append(text));
        return visible.ToString();
    }
    internal void Append(string fragment, Action<string> onText)
    {
        pending += fragment;
        while(pending.Length > 0)
        {
            int index = -1;
            (string Tag, bool? Thinking) match = (string.Empty, null);
            foreach(var tag in Tags)
            {
                int candidate = pending.IndexOf(tag.Tag, StringComparison.Ordinal);
                if(candidate >= 0 && (index < 0 || candidate < index)) { index = candidate; match = tag; }
            }
            if(index >= 0)
            {
                AppendContent(onText, pending.AsSpan(0, index));
                pending = pending[(index + match.Tag.Length)..];
                if(match.Thinking is bool state) thinking = state;
                continue;
            }
            int keep = 0;
            foreach(var tag in Tags)
                for(int count = 1; count < tag.Tag.Length && count <= pending.Length; count++)
                    if(count > keep && pending.EndsWith(tag.Tag[..count], StringComparison.Ordinal)) keep = count;
            AppendContent(onText, pending.AsSpan(0, pending.Length - keep));
            pending = keep == 0 ? string.Empty : pending[^keep..];
            break;
        }
    }
    public string Flush()
    {
        if(thinking && pending.Length > 0) onReasoning?.Invoke(pending);
        string result = thinking ? string.Empty : pending;
        pending = string.Empty;
        return result;
    }
    private void AppendContent(Action<string> onText, ReadOnlySpan<char> content)
    {
        if(content.Length == 0) return;
        if(thinking) onReasoning?.Invoke(content.ToString());
        else onText(content.ToString());
    }
}
