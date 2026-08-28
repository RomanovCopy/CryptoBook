using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

/// <summary>
/// Optional fast path between a remote provider and local storage. Providers
/// with more than one physical device also identify device boundaries so the
/// transfer engine can stage cross-device copies safely.
/// </summary>
public interface ILocalTransferProvider
{
    Task<FileOperationResult> PullToLocalAsync(
        StorageLocation source,
        StorageLocation localDestination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);
    Task<FileOperationResult> PushFromLocalAsync(
        StorageLocation localSource,
        StorageLocation destination,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);
    bool IsSameDevice(StorageLocation left, StorageLocation right);
}
