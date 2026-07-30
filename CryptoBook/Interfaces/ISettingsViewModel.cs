using CryptoBook.DTO;

using System.Windows.Input;
using System.Windows;

namespace CryptoBook.Interfaces
{
    public interface ISettingsViewModel:
        IViewModel,
        IWindowWithId,
        ICloseable
    {
        IReadOnlyList<ApplicationThemeOption> Themes { get; }
        ApplicationThemeOption SelectedTheme { get; set; }
        GridLength NavigationPaneWidth { get; set; }
        int SelectedSectionIndex { get; set; }
        string WorkspaceDirectory { get; }
        string SearchQuery { get; set; }
        IReadOnlyList<WorkspaceSearchResult> SearchResults { get; }
        bool IsSearching { get; }
        string SearchStatus { get; }

        ICommand ChooseWorkspace { get; }
        ICommand Search { get; }
        ICommand OpenSearchResult { get; }
        ICommand RevealSearchResult { get; }
    }
}
