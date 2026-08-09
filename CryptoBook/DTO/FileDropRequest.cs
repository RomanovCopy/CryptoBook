namespace CryptoBook.DTO
{
    public sealed record FileDropRequest(
        IReadOnlyList<string> SourcePaths,
        string DestinationDirectory,
        FileTransferKind Operation);
}
