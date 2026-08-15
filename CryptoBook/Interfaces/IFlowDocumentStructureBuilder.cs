using CryptoBook.DTO;

using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IFlowDocumentStructureBuilder: IService
    {
        DocumentStructureNode Build(
            FlowDocument document,
            bool includeTextElements);
    }
}
