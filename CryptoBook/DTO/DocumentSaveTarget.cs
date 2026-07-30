using CryptoBook.Interfaces;

namespace CryptoBook.DTO
{
    public sealed record DocumentSaveTarget(
        string FilePath,
        IFileTemplate Template);
}
