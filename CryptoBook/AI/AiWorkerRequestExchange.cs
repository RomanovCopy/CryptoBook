using System.IO;

namespace CryptoBook.AI;

/// <summary>One request/response exchange; drains cancellation acknowledgement before reuse.</summary>
internal static class AiWorkerRequestExchange
{
    private static long sequence;

    internal static async Task<AiModelInspection?> ExecuteAsync(Stream input, Stream output,
        AiWorkerRequest request, Action stop, Action<string> onText, IProgress<float>? progress,
        CancellationToken cancellationToken, Action<string>? onReasoning = null,
        Action<AiTokenUsage>? onUsage = null, Action<AiPerformanceMeasurement>? onMeasurement = null,
        TimeSpan? cancellationTimeout = null)
    {
        bool cooperative = request.Command == "generate";
        if(cancellationToken.IsCancellationRequested && cooperative) throw new AiWorkerCancelledException(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        request = request with { RequestId = Interlocked.Increment(ref sequence) };
        using var monitorStop = new CancellationTokenSource();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() =>
        {
            if(cooperative) cancelled.TrySetResult(); else stop();
        });
        int finished = 0;
        CancellationToken ioToken = cooperative ? CancellationToken.None : cancellationToken;
        Task write = Task.Run(() => AiWorkerProtocol.WriteAsync(input, request, ioToken));
        Task monitor = cooperative ? CancelAsync() : Task.CompletedTask;
        try
        {
            await write.ConfigureAwait(false);
            AiModelInspection? inspection = null;
            while(true)
            {
                var message = await AiWorkerProtocol.ReadAsync<AiWorkerEvent>(output, ioToken).ConfigureAwait(false)
                    ?? throw new EndOfStreamException("The AI worker closed its response pipe.");
                switch(message.Kind)
                {
                    case "text": if(!cancellationToken.IsCancellationRequested) onText(message.Text ?? ""); break;
                    case "reasoning": if(!cancellationToken.IsCancellationRequested) onReasoning?.Invoke(message.Text ?? ""); break;
                    case "usage": if(!cancellationToken.IsCancellationRequested && message.Usage is not null) onUsage?.Invoke(message.Usage); break;
                    case "progress": if(!cancellationToken.IsCancellationRequested) progress?.Report(message.Progress); break;
                    case "inspection": inspection = message.Inspection; break;
                    case "measurement":
                        if(message.Measurement is not { IsValid: true }) throw new AiWorkerException("Protocol");
                        onMeasurement?.Invoke(message.Measurement); break;
                    case "done":
                        Interlocked.Exchange(ref finished, 1);
                        if(cooperative && cancellationToken.IsCancellationRequested) throw new AiWorkerCancelledException(cancellationToken);
                        return inspection;
                    case "cancelled":
                        if(!cooperative || !cancellationToken.IsCancellationRequested) throw new AiWorkerException("Protocol");
                        Interlocked.Exchange(ref finished, 1);
                        throw new AiWorkerCancelledException(cancellationToken);
                    case "repetition":
                        if(message.Repetition is not { KeepCharacters: >= 0 } repetition) throw new AiWorkerException("Protocol");
                        Interlocked.Exchange(ref finished, 1);
                        if(cooperative && cancellationToken.IsCancellationRequested) throw new AiWorkerCancelledException(cancellationToken);
                        throw new AiRepetitionException(repetition);
                    case "error":
                        if(message.Text == "AI.ResourcePressure") stop();
                        throw new AiException(message.Text is not null && message.Text.StartsWith("AI.", StringComparison.Ordinal)
                            ? message.Text : "AI.GenerationFailed");
                    default: throw new AiWorkerException("Protocol");
                }
            }
        }
        catch(Exception error) when(cancellationToken.IsCancellationRequested && error is IOException or ObjectDisposedException)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        finally
        {
            monitorStop.Cancel();
            await monitor.ConfigureAwait(false);
        }

        async Task CancelAsync()
        {
            try
            {
                await cancelled.Task.WaitAsync(monitorStop.Token).ConfigureAwait(false);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(monitorStop.Token);
                deadline.CancelAfter(cancellationTimeout ?? TimeSpan.FromSeconds(5));
                try
                {
                    // Never interleave the cancel frame with a partially written request.
                    await write.WaitAsync(deadline.Token).ConfigureAwait(false);
                    await AiWorkerProtocol.WriteAsync(input, request with
                    {
                        Command = "cancel", Settings = new(), Turns = [], ProtectResources = false,
                        Options = request.Options with { PerformanceBoost = false }
                    }, deadline.Token).ConfigureAwait(false);
                    await Task.Delay(Timeout.InfiniteTimeSpan, deadline.Token).ConfigureAwait(false);
                }
                catch(OperationCanceledException) when(!monitorStop.IsCancellationRequested)
                { if(Volatile.Read(ref finished) == 0) stop(); }
            }
            catch(OperationCanceledException) when(monitorStop.IsCancellationRequested) { }
            catch(Exception error) when(error is IOException or ObjectDisposedException)
            { if(Volatile.Read(ref finished) == 0) stop(); }
        }
    }
}
