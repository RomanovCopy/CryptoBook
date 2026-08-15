using CryptoBook.DTO;

using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IDocumentStructureViewModel: IViewModel
    {
        ObservableCollection<DocumentStructureNode> Nodes { get; }
        bool IsOpen { get; }
        bool IncludeTextElements { get; set; }
        bool HasNodes { get; }
        bool IsEditingEnabled { get; }

        ICommand ToggleCommand { get; }
        ICommand RefreshCommand { get; }
        ICommand NavigateCommand { get; }
        ICommand AddParagraphCommand { get; }
        ICommand AddParagraphBeforeCommand { get; }
        ICommand AddParagraphInsideCommand { get; }
        ICommand AddParagraphAfterCommand { get; }
        ICommand MoveCommand { get; }
        ICommand MoveUpCommand { get; }
        ICommand MoveDownCommand { get; }
        ICommand DeleteCommand { get; }
    }
}
