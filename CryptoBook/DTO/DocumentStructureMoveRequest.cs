namespace CryptoBook.DTO
{
    public sealed record DocumentStructureMoveRequest(
        DocumentStructureNode Source,
        DocumentStructureNode Target,
        DocumentStructureDropPosition Position);
}
