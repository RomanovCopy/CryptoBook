namespace CryptoBook.Interfaces;

public interface IAiAttachmentPicker : IService
{
    Task<IReadOnlyList<string>> PickAttachmentsAsync(CancellationToken cancellationToken = default);
}
