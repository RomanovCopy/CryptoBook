using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IDocumentPrintService: IService
    {
        void Print(FlowDocument document, string documentName);
    }
}
