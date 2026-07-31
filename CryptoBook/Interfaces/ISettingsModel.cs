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
        int SelectedLanguageIndex { get; set; }
        bool IsEnglishSelected { get; set; }
        bool IsRussianSelected { get; set; }
        IReadOnlyList<string> LanguageDisplayNames { get; }
        GridLength NavigationPaneWidth { get; set; }
        int SelectedSectionIndex { get; set; }
        string WorkspaceDirectory { get; }
        string SearchQuery { get; set; }
        IReadOnlyList<WorkspaceSearchResult> SearchResults { get; }
        bool IsSearching { get; }
        string SearchStatus { get; }

        Task ChooseWorkspaceAsync();
        Task SearchAsync();
        void OpenSearchResult(WorkspaceSearchResult? result);
        void RevealSearchResult(WorkspaceSearchResult? result);
        void Close();
        void Closing();
        void Closed();
    }
}
