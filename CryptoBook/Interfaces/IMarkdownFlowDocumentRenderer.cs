using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IMarkdownFlowDocumentRenderer: IService
    {
        FlowDocument Render(string markdown, string? markdownFilePath);
    }
}
