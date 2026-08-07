using CryptoBook.Security;

namespace CryptoBook.Interfaces;

public interface IKeyResetService : IService, IDisposable
{
    KeyResetState State { get; }
    TimeSpan Timeout { get; }
    bool IsPaused { get; }
    event EventHandler<KeyResetStateChangedEventArgs>? StateChanged;
    event EventHandler<Exception>? SnapshotFailed;

    void Start();
    void Stop();
    void NotifyActivity();
    void UpdateTimeout(TimeSpan timeout);
    IDisposable Pause();
    Task<bool> ResetAsync(CancellationToken cancellationToken = default);
    Task<bool> TryUnlockAsync(string key, CancellationToken cancellationToken = default);
    Task RestoreSnapshotAsync(bool restoreAsUnsaved, CancellationToken cancellationToken = default);
}

public sealed class KeyResetStateChangedEventArgs(KeyResetState state) : EventArgs
{
    public KeyResetState State { get; } = state;
}
