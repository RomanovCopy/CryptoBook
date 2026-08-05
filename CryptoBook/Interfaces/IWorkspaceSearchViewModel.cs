using CryptoBook.DTO;

using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IWorkspaceSearchViewModel: IPageViewModel
    {
        string SearchQuery { get; set; }
        string SearchStatus { get; }
        string CurrentFile { get; }
        bool IsSearching { get; }
        IReadOnlyList<WorkspaceContentSearchResult> SearchResults { get; }

        ICommand Search { get; }
        ICommand CancelSearch { get; }
        ICommand OpenResult { get; }
        ICommand DeleteResult { get; }
        ICommand ClearPage { get; }
        ICommand ClosePage { get; }
    }
}
