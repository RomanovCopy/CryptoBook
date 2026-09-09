using CryptoBook.DTO;

using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMarkdownSyntaxHelpViewModel: IPageViewModel
    {
        IReadOnlyList<MarkdownSyntaxHelpGroup> Groups { get; }
        ICommand BackToEditor { get; }
    }
}
