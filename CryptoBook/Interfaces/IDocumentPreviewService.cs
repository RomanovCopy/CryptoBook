using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IDocumentPreviewService: IService
    {
        FlowDocument CreatePreview(FlowDocument source);
    }
}
