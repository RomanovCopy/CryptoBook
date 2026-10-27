using System.IO;
using System.Threading.Channels;

namespace CryptoBook.AI;

/// <summary>Reads control frames while the native engine is processing a request.</summary>
internal sealed class AiWorkerRequestInbox : IDisposable
{
    private readonly Stream input;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<Operation> requests = Channel.CreateBounded<Operation>(1);
    private readonly Dictionary<long, Operation> pending = [];
    private readonly object sync = new();
    private bool disposed;

    internal sealed class Operation(AiWorkerRequest request, CancellationTokenSource cancellation)
    {
        internal long Id { get; } = request.RequestId;
        internal AiWorkerRequest Request { get; private set; } = request;
        internal CancellationTokenSource Cancellation { get; } = cancellation;
        internal CancellationToken Token { get; } = cancellation.Token;
        internal void Release() { Request = null!; Cancellation.Dispose(); }
    }

    internal AiWorkerRequestInbox(Stream input)
    {
        this.input = input;
        _ = Task.Run(ReadAsync);
    }

    internal IAsyncEnumerable<Operation> ReadAllAsync() => requests.Reader.ReadAllAsync();

    private async Task ReadAsync()
    {
        Exception? failure = null;
        try
        {
            while(!lifetime.IsCancellationRequested)
            {
                var request = await AiWorkerProtocol.ReadAsync<AiWorkerRequest>(input, lifetime.Token).ConfigureAwait(false);
                if(request is null) break;
                lock(sync)
                {
                    if(disposed) return;
                    if(request.Command == "cancel")
                    {
                        if(pending.TryGetValue(request.RequestId, out var active)) active.Cancellation.Cancel();
                        continue;
                    }
                    var operation = new Operation(request, CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token));
                    bool added = pending.TryAdd(request.RequestId, operation);
                    if(!added || !requests.Writer.TryWrite(operation))
                    {
                        if(added) pending.Remove(request.RequestId);
                        operation.Release();
                        throw new IOException("Invalid AI worker request sequence.");
                    }
                    request = null!;
                }
            }
        }
        catch(Exception error) when(error is IOException or OperationCanceledException or ObjectDisposedException or System.Text.Json.JsonException)
        { failure = error; }
        finally
        {
            requests.Writer.TryComplete(failure);
            // Losing the parent pipe must interrupt an active native request as well.
            lifetime.Cancel();
        }
    }

    internal void Complete(Operation operation)
    {
        lock(sync)
        {
            if(pending.Remove(operation.Id)) operation.Release();
        }
    }

    public void Dispose()
    {
        lock(sync)
        {
            if(disposed) return;
            disposed = true;
            lifetime.Cancel();
            requests.Writer.TryComplete();
            foreach(var operation in pending.Values) operation.Release();
            pending.Clear();
        }
        // The owning worker closes input after disposal, unblocking synchronous stdin reads.
        // Do not wait for an OS read that may not support cancellation.
    }
}
