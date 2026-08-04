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
        private const string FeedbackAddress = "EncryptoBook@gmail.com";
        private static readonly Uri FeedbackUri = new(
            $"mailto:{FeedbackAddress}");
        private readonly ISettingsModel model;
        private readonly IUriNavigationService uriNavigationService;

        public SettingsViewModel(
            ISettingsModel model,
            IUriNavigationService uriNavigationService)
        {
            this.model = model ??
                throw new ArgumentNullException(nameof(model));
            this.uriNavigationService = uriNavigationService ??
                throw new ArgumentNullException(nameof(uriNavigationService));
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

        public string FeedbackEmail => FeedbackAddress;

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

        public ICommand SendFeedback => sendFeedback ??=
            new RelayCommand(_ => uriNavigationService.TryOpen(FeedbackUri));
        private RelayCommand? sendFeedback;

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
