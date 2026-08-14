using CryptoBook.DTO;

using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IFlowDocumentMoveService: IService
    {
        bool CanMove(
            FlowDocument document,
            TextElement source,
            FrameworkContentElement target,
            DocumentStructureDropPosition position);

        bool Move(
            FlowDocument document,
            TextElement source,
            FrameworkContentElement target,
            DocumentStructureDropPosition position);
    }
}
