namespace CryptoBook.Interfaces
{
    public interface IDocumentLayoutMetrics: IService
    {
        double AvailableWidth { get; }
        double AvailableHeight { get; }
    }
}
