using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IDocumentContentInspector: IService
    {
        bool HasPrintableContent(FlowDocument document);
    }
}
