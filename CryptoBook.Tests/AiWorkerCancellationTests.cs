using CryptoBook.AI;
using System.IO;
using System.IO.Pipes;
using Xunit;

namespace CryptoBook.Tests;

public sealed class AiWorkerCancellationTests
{
    private static AiWorkerRequest Generate => new("generate", new(), new(1024, 0, CpuBackend: true), []);

    [Fact]
    public async Task Completion_KeepsConnectionAndAcceptsSubsequentRequests()
    {
        int calls = 0;
        await using var worker = await Host.Create(async (operation, send) =>
        {
            await send(new("text", Text: (++calls).ToString()));
            await send(new("done"));
        });
        var text = new List<string>();
        for(int i = 0; i < 3; i++) await worker.Execute(Generate, text.Add);
        Assert.Equal(new[] { "1", "2", "3" }, text);
        Assert.Equal(0, worker.StopCount);
        Assert.Equal(3, worker.Requests.Select(request => request.RequestId).Distinct().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_DrainsAcknowledgementAndLateFragments_ThenReusesConnection(bool finishesNormally)
    {
        int calls = 0;
        bool nativeCancellationObserved = false;
        await using var worker = await Host.Create(async (operation, send) =>
        {
            if(++calls == 1)
            {
                await send(new("text", Text: "partial"));
                try { await Task.Delay(Timeout.Infinite, operation.Token); }
                catch(OperationCanceledException)
                {
                    nativeCancellationObserved = true;
                    await send(new("text", Text: "late"));
                    await send(new(finishesNormally ? "done" : "cancelled"));
                }
            }
            else { await send(new("text", Text: "next")); await send(new("done")); }
        });
        using var stop = new CancellationTokenSource();
        var text = new List<string>();
        await Assert.ThrowsAsync<AiWorkerCancelledException>(() => worker.Execute(Generate,
            value => { text.Add(value); stop.Cancel(); }, stop.Token));
        Assert.True(nativeCancellationObserved);
        Assert.Equal(0, worker.StopCount);
        await worker.Execute(Generate, text.Add);
        Assert.Equal(new[] { "partial", "next" }, text);
        Assert.Equal(0, worker.StopCount);
    }

    [Fact]
    public async Task LateCancel_CannotCancelTheNextRequest()
    {
        var nextStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        await using var worker = await Host.Create(async (operation, send) =>
        {
            if(++calls == 2)
            {
                nextStarted.SetResult();
                await finish.Task.WaitAsync(operation.Token);
                operation.Token.ThrowIfCancellationRequested();
            }
            await send(new("done"));
        });
        await worker.Execute(Generate);
        var previous = Assert.Single(worker.Requests);
        Task second = worker.Execute(Generate);
        await nextStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await AiWorkerProtocol.WriteAsync(worker.Client, previous with { Command = "cancel" }, CancellationToken.None);
        finish.SetResult();
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.Execute(Generate);
        Assert.Equal(3, calls);
        Assert.Equal(0, worker.StopCount);
    }

    [Fact]
    public async Task StopBeforeExecution_CancelsTheQueuedRequestOnly()
    {
        await using var pipes = await Pipes.Create();
        using var inbox = new AiWorkerRequestInbox(pipes.Server);
        await AiWorkerProtocol.WriteAsync(pipes.Client, Generate with { RequestId = 11 }, CancellationToken.None);
        await AiWorkerProtocol.WriteAsync(pipes.Client, Generate with { Command = "cancel", RequestId = 11 }, CancellationToken.None);
        await using var reader = inbox.ReadAllAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        var first = reader.Current;
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var registration = first.Token.Register(() => cancelled.TrySetResult()))
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(first.Token.IsCancellationRequested);
        inbox.Complete(first);
        await AiWorkerProtocol.WriteAsync(pipes.Client, Generate with { Command = "cancel", RequestId = 11 }, CancellationToken.None);
        await AiWorkerProtocol.WriteAsync(pipes.Client, Generate with { RequestId = 12 }, CancellationToken.None);
        Assert.True(await reader.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(12, reader.Current.Request.RequestId);
        Assert.False(reader.Current.Token.IsCancellationRequested);
        inbox.Complete(reader.Current);
    }

    [Fact]
    public async Task CompletionAfterInboxDisposal_IsSafeAndIdempotent()
    {
        await using var pipes = await Pipes.Create();
        using var inbox = new AiWorkerRequestInbox(pipes.Server);
        await AiWorkerProtocol.WriteAsync(pipes.Client, Generate with { RequestId = 11 }, CancellationToken.None);
        await using var reader = inbox.ReadAllAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        var operation = reader.Current;
        inbox.Dispose();
        inbox.Complete(operation);
        inbox.Complete(operation);
        Assert.True(operation.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task UnresponsiveNativeCall_IsForcedToStopAfterGracePeriod()
    {
        await using var worker = await Host.Create(async (_, send) =>
        {
            await send(new("text", Text: "partial"));
            await Task.Delay(Timeout.Infinite); // Ignores the operation token, like a hung native call.
        });
        using var stop = new CancellationTokenSource();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            worker.Execute(Generate, _ => stop.Cancel(), stop.Token, TimeSpan.FromMilliseconds(100))
                .WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsNotType<AiWorkerCancelledException>(error);
        Assert.Equal(1, worker.StopCount);
    }

    [Fact]
    public async Task UnsolicitedCancellationAcknowledgement_IsRejected()
    {
        await using var worker = await Host.Create((_, send) => send(new("cancelled")));
        await Assert.ThrowsAsync<AiWorkerException>(() => worker.Execute(Generate));
    }

    [Fact]
    public async Task LoadingCancellation_StillTerminatesTheWorkerImmediately()
    {
        await using var worker = await Host.Create(async (_, send) =>
        {
            await send(new("text", Text: "loading"));
            await Task.Delay(Timeout.Infinite);
        });
        using var stop = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.Execute(Generate with { Command = "load" },
            _ => stop.Cancel(), stop.Token).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, worker.StopCount);
    }

    private sealed class Pipes : IAsyncDisposable
    {
        internal NamedPipeServerStream Server { get; }
        internal NamedPipeClientStream Client { get; }
        private Pipes(string name)
        {
            Server = new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            Client = new(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        }
        internal static async Task<Pipes> Create()
        {
            var pipes = new Pipes("CryptoBook-Cancel-" + Guid.NewGuid().ToString("N"));
            await Task.WhenAll(pipes.Server.WaitForConnectionAsync(), pipes.Client.ConnectAsync(5000));
            return pipes;
        }
        public ValueTask DisposeAsync() { Client.Dispose(); Server.Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class Host : IAsyncDisposable
    {
        private readonly Pipes pipes;
        private readonly AiWorkerRequestInbox inbox;
        private readonly Task running;
        internal NamedPipeClientStream Client => pipes.Client;
        internal List<AiWorkerRequest> Requests { get; } = [];
        internal int StopCount { get; private set; }
        private Host(Pipes pipes, Func<AiWorkerRequestInbox.Operation, Func<AiWorkerEvent, Task>, Task> handle)
        {
            this.pipes = pipes;
            inbox = new(pipes.Server);
            running = Task.Run(async () =>
            {
                try
                {
                    await foreach(var operation in inbox.ReadAllAsync())
                    {
                        Requests.Add(operation.Request);
                        try { await handle(operation, Send).WaitAsync(operation.Token); }
                        catch(OperationCanceledException) when(operation.Token.IsCancellationRequested) { }
                        finally { inbox.Complete(operation); }
                    }
                }
                catch(Exception error) when(error is IOException or ObjectDisposedException or OperationCanceledException) { }
            });
        }
        private Task Send(AiWorkerEvent message) => AiWorkerProtocol.WriteAsync(pipes.Server, message, CancellationToken.None);
        private void Stop()
        {
            StopCount++;
            pipes.Client.Dispose();
            pipes.Server.Dispose();
        }
        internal static async Task<Host> Create(Func<AiWorkerRequestInbox.Operation, Func<AiWorkerEvent, Task>, Task> handle)
            => new(await Pipes.Create(), handle);
        internal Task Execute(AiWorkerRequest request, Action<string>? text = null, CancellationToken token = default, TimeSpan? grace = null)
            => AiWorkerRequestExchange.ExecuteAsync(pipes.Client, pipes.Client, request, Stop, text ?? (_ => { }), null, token, cancellationTimeout: grace);
        public async ValueTask DisposeAsync()
        {
            await pipes.DisposeAsync();
            inbox.Dispose();
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
