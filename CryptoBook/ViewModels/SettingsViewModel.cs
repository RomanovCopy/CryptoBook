using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Windows.Input;
using System.Windows;

namespace CryptoBook.ViewModels
{
    public sealed class SettingsViewModel:
        ViewModelBase,
        ISettingsViewModel
    {
        private readonly ISettingsModel model;

        public SettingsViewModel(ISettingsModel model)
        {
            this.model = model ??
                throw new ArgumentNullException(nameof(model));
            model.PropertyChanged += (_, args) =>
                OnPropertyChanged(args.PropertyName ?? string.Empty);
        }

        event EventHandler ICloseable.RequestClose
        {
            add { }
            remove { }
        }

        public Guid WindowId => model.WindowId;

        public IReadOnlyList<ApplicationThemeOption> Themes =>
            model.Themes;

        public ApplicationThemeOption SelectedTheme
        {
            get => model.SelectedTheme;
            set => model.SelectedTheme = value;
        }

        public IReadOnlyList<ApplicationLanguageOption> Languages =>
            model.Languages;

        public ApplicationLanguageOption SelectedLanguage
        {
            get => model.SelectedLanguage;
            set => model.SelectedLanguage = value;
        }

        public string SelectedCultureName
        {
            get => model.SelectedCultureName;
            set => model.SelectedCultureName = value;
        }

        public int SelectedLanguageIndex
        {
            get => model.SelectedLanguageIndex;
            set => model.SelectedLanguageIndex = value;
        }

        public bool IsEnglishSelected
        {
            get => model.IsEnglishSelected;
            set => model.IsEnglishSelected = value;
        }

        public bool IsRussianSelected
        {
            get => model.IsRussianSelected;
            set => model.IsRussianSelected = value;
        }

        public IReadOnlyList<string> LanguageDisplayNames =>
            model.LanguageDisplayNames;

        public GridLength NavigationPaneWidth
        {
            get => model.NavigationPaneWidth;
            set => model.NavigationPaneWidth = value;
        }

        public int SelectedSectionIndex
        {
            get => model.SelectedSectionIndex;
            set => model.SelectedSectionIndex = value;
        }

        public string WorkspaceDirectory => model.WorkspaceDirectory;

        public string SearchQuery
        {
            get => model.SearchQuery;
            set => model.SearchQuery = value;
        }

        public IReadOnlyList<WorkspaceSearchResult> SearchResults =>
            model.SearchResults;

        public bool IsSearching => model.IsSearching;

        public string SearchStatus => model.SearchStatus;

        public ICommand ChooseWorkspace => chooseWorkspace ??=
            new RelayCommand(async _ => await model.ChooseWorkspaceAsync());
        private RelayCommand? chooseWorkspace;

        public ICommand Search => search ??=
            new RelayCommand(async _ => await model.SearchAsync());
        private RelayCommand? search;

        public ICommand OpenSearchResult => openSearchResult ??=
            new AsyncRelayCommand(
                (parameter, token) => model.OpenSearchResultAsync(
                    parameter as WorkspaceSearchResult,
                    token));
        private AsyncRelayCommand? openSearchResult;

        public ICommand RevealSearchResult => revealSearchResult ??=
            new RelayCommand(
                parameter => model.RevealSearchResult(
                    parameter as WorkspaceSearchResult));
        private RelayCommand? revealSearchResult;

        public ICommand Loaded => loaded ??=
            new RelayCommand(_ => { });
        private RelayCommand? loaded;

        public ICommand Close => close ??=
            new RelayCommand(_ => model.Close());
        private RelayCommand? close;

        public ICommand Closing => closing ??=
            new RelayCommand(_ => model.Closing());
        private RelayCommand? closing;

        public ICommand Closed => closed ??=
            new RelayCommand(_ => model.Closed());
        private RelayCommand? closed;
    }
}
