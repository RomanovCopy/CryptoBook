using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Reflection;
using System.Windows.Input;
using System.Windows;

namespace CryptoBook.ViewModels
{
    public sealed class SettingsViewModel:
        ViewModelBase,
        ISettingsViewModel
    {
        private readonly ISettingsModel model;
        private readonly IUpdateNotificationViewModel updateNotification;

        public SettingsViewModel(
            ISettingsModel model,
            IUpdateNotificationViewModel updateNotification)
        {
            this.model = model ??
                throw new ArgumentNullException(nameof(model));
            this.updateNotification = updateNotification ??
                throw new ArgumentNullException(nameof(updateNotification));
            model.PropertyChanged += (_, args) =>
                OnPropertyChanged(args.PropertyName ?? string.Empty);
            updateNotification.PropertyChanged += (_, args) =>
            {
                if(args.PropertyName == nameof(updateNotification.CheckStatus))
                    OnPropertyChanged(nameof(UpdateCheckStatus));
            };
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

        public string ApplicationVersion { get; } = GetApplicationVersion();

        public string UpdateCheckStatus => updateNotification.CheckStatus;

        public IUpdateNotificationViewModel UpdateNotification =>
            updateNotification;

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

        public IReadOnlyList<KeyResetIntervalOption> KeyResetIntervals =>
            model.KeyResetIntervals;

        public KeyResetIntervalOption SelectedKeyResetInterval
        {
            get => model.SelectedKeyResetInterval;
            set => model.SelectedKeyResetInterval = value;
        }

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

        public ICommand CheckForUpdates => checkForUpdates ??=
            new AsyncRelayCommand(
                (_, token) => updateNotification.CheckNowAsync(token));
        private AsyncRelayCommand? checkForUpdates;

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

        private static string GetApplicationVersion()
        {
            Assembly assembly = typeof(SettingsViewModel).Assembly;
            string? informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if(!string.IsNullOrWhiteSpace(informationalVersion))
            {
                string releaseVersion = informationalVersion
                    .Split('+', 2)[0];
                if(!string.IsNullOrWhiteSpace(releaseVersion))
                    return releaseVersion;
            }

            Version? version = assembly.GetName().Version;
            if(version is null)
                return "—";

            return version.Revision > 0
                ? version.ToString(4)
                : version.Build >= 0
                    ? version.ToString(3)
                    : version.ToString(2);
        }
    }
}
