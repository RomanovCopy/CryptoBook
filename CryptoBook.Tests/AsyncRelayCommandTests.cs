using CryptoBook.Infrastructure;

using Xunit;

namespace CryptoBook.Tests;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task Command_DisablesItselfWhileRunning()
    {
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new AsyncRelayCommand(async (_, token) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(token);
        });

        Task execution = command.ExecuteAsync();
        await started.Task;

        Assert.True(command.IsRunning);
        Assert.False(command.CanExecute(null));

        release.SetResult();
        await execution;

        Assert.False(command.IsRunning);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_PropagatesCancellationToOperation()
    {
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new AsyncRelayCommand(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });

        Task execution = command.ExecuteAsync();
        await started.Task;
        command.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await execution);
        Assert.False(command.IsRunning);
    }
}
