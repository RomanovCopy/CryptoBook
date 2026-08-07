using CryptoBook.DTO;

using System.ComponentModel;
using System.Windows;

namespace CryptoBook.Interfaces
{
    public interface ISettingsModel: INotifyPropertyChanged
    {
        Guid WindowId { get; }
        IReadOnlyList<ApplicationThemeOption> Themes { get; }
        ApplicationThemeOption SelectedTheme { get; set; }
        IReadOnlyList<ApplicationLanguageOption> Languages { get; }
        ApplicationLanguageOption SelectedLanguage { get; set; }
        string SelectedCultureName { get; set; }
        GridLength NavigationPaneWidth { get; set; }
        int SelectedSectionIndex { get; set; }
        string WorkspaceDirectory { get; }
        string SearchQuery { get; set; }
        IReadOnlyList<WorkspaceSearchResult> SearchResults { get; }
        bool IsSearching { get; }
        string SearchStatus { get; }
        IReadOnlyList<KeyResetIntervalOption> KeyResetIntervals { get; }
        KeyResetIntervalOption SelectedKeyResetInterval { get; set; }

        Task ChooseWorkspaceAsync();
        Task SearchAsync();
        Task OpenSearchResultAsync(
            WorkspaceSearchResult? result,
            CancellationToken cancellationToken = default);
        void RevealSearchResult(WorkspaceSearchResult? result);
        void OpenEncryptionKeyDialog();
        void Close();
        void Closing();
        void Closed();
    }
}
