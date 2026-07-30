using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Загружает, сохраняет и создаёт начальное содержимое конкретного
    /// формата FlowDocument.
    /// </summary>
    public interface IDocumentFormatHandler: IService
    {
        bool CanHandle(IFileTemplate template);

        Task LoadAsync(
            FlowDocument document,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default);

        Task<byte[]> SerializeAsync(
            FlowDocument document,
            CancellationToken cancellationToken = default);
    }
}
