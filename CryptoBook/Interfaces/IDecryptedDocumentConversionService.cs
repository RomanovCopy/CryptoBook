using CryptoBook.Security;

using System.IO;

namespace CryptoBook.Interfaces
{
    public interface IDecryptedDocumentConversionService: IService
    {
        bool CanConvert(string originalExtension);

        Task ConvertAsync(
            Stream source,
            string originalExtension,
            DecryptionOutputFormat targetFormat,
            Stream destination,
            CancellationToken cancellationToken = default);
    }
}
