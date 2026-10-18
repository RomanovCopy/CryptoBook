using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using LLama.Transformers;
using System.Text;
using System.Runtime.CompilerServices;

namespace CryptoBook.AI;

/// <summary>In-process inference only: no server, HTTP, child process or saved chat.</summary>
public sealed class LocalAiInferenceService : IAiInferenceService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private LLamaWeights? weights;
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

    public async Task GenerateAsync(AiModelSettings settings, IReadOnlyList<AiTurn> conversation,
        Action<string> onText, IProgress<float>? loadProgress, CancellationToken cancellationToken)
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
                BatchSize = 256,
                UBatchSize = 128,
                Threads = Math.Max(1, Environment.ProcessorCount / 2),
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
                            settings.MemoryMode == AiMemoryMode.Automatic && parameters.GpuLayerCount > 0 && attempt < 2)
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
            if(projectorPath is not null && (projector is null || loadedProjectorPath != projectorPath))
            {
                projector?.Dispose(); projector = null;
                var visionParameters = MtmdContextParams.Default();
                // Keep the vision encoder in RAM so model layers and context can share the GPU budget.
                visionParameters.UseGpu = false;
                visionParameters.Warmup = false;
                visionParameters.NThreads = Math.Max(1, Environment.ProcessorCount / 2);
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
                if(architecture == "gemma4") prompt = AiChatFormat.Gemma4(settings, promptTurns, mediaMarker);
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
                    if(images.Length > 0)
                    {
                        foreach(var image in images) embeds.Add(projector!.LoadMedia(image.ImageData.Span));
                        int result = projector!.Tokenize(prompt, true, true, embeds.ToArray(), out imageChunks);
                        if(result != 0 || imageChunks is null) throw new AiException("AI.ImageLoadFailed");
                        inputTokens = checked((int)Math.Max(imageChunks.CountTokens(), (ulong)imageChunks.CountPositions()));
                    }
                    else inputTokens = weights.Tokenize(prompt, add_bos: true, special: true, Encoding.UTF8).Length;
                    int outputTokens = settings.OutputTokenLimit(inputTokens);
                    using var sampling = new DefaultSamplingPipeline { Temperature = settings.Temperature };
                    IAsyncEnumerable<string> generation;
                    if(images.Length > 0)
                    {
                        context = weights.CreateContext(parameters);
                        generation = InferVisionAsync(context, imageChunks!, sampling, outputTokens, cancellationToken);
                    }
                    else
                    {
                        var executor = new StatelessExecutor(weights, parameters);
                        context = executor.Context;
                        generation = executor.InferAsync(prompt, new InferenceParams { MaxTokens = outputTokens,
                            SamplingPipeline = sampling, OverflowStrategy = ContextOverflowStrategy.ThrowException }, cancellationToken);
                    }
                    var filter = new AiThinkingFilter();
                    await foreach(string fragment in generation.ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string visible = filter.Append(fragment);
                        if(visible.Length > 0) onText(visible);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    string remaining = filter.Flush();
                    if(remaining.Length > 0) onText(remaining);
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
        finally
        {
            lock(cancellationLock) activeCancellation = null;
            LastUsedUtc = DateTime.UtcNow;
            IsBusy = false;
            gate.Release();
        }
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

    private void UnloadCore()
    {
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

/// <summary>Removes thinking blocks even when tags span streamed fragments.</summary>
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
    public string Append(string fragment)
    {
        pending += fragment;
        var visible = new StringBuilder();
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
                if(!thinking) visible.Append(pending.AsSpan(0, index));
                pending = pending[(index + match.Tag.Length)..];
                if(match.Thinking is bool state) thinking = state;
                continue;
            }
            int keep = 0;
            foreach(var tag in Tags)
                for(int count = 1; count < tag.Tag.Length && count <= pending.Length; count++)
                    if(count > keep && pending.EndsWith(tag.Tag[..count], StringComparison.Ordinal)) keep = count;
            if(!thinking) visible.Append(pending.AsSpan(0, pending.Length - keep));
            pending = keep == 0 ? string.Empty : pending[^keep..];
            break;
        }
        return visible.ToString();
    }
    public string Flush()
    {
        string result = thinking ? string.Empty : pending;
        pending = string.Empty;
        return result;
    }
}
