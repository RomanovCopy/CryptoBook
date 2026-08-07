using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System.Windows;

namespace CryptoBook.Models
{
    /// <summary>
    /// Состояние окна настроек: тема, язык, рабочая папка и поиск файлов
    /// с отменой предыдущего запроса при запуске нового.
    /// </summary>
    public sealed class SettingsModel:
        ViewModelBase,
        ISettingsModel
    {
        private readonly IThemeManager themeManager;
        private readonly IWindowManager windowManager;
        private readonly IWorkspaceService? workspaceService;
        private readonly IFolderPickerService? folderPickerService;
        private readonly IFileLauncherService? fileLauncherService;
        private readonly IWorkspaceFileOpenService? workspaceFileOpenService;
        private readonly IKeyResetService? keyResetService;
        private ApplicationThemeOption selectedTheme;
        private ApplicationLanguageOption selectedLanguage;
        private GridLength navigationPaneWidth;
        private CancellationTokenSource? searchCancellation;
        private int selectedSectionIndex;
        private string searchQuery = string.Empty;
        private IReadOnlyList<WorkspaceSearchResult> searchResults =
            Array.Empty<WorkspaceSearchResult>();
        private bool isSearching;
        private string searchStatus = string.Empty;
        private KeyResetIntervalOption selectedKeyResetInterval;

        public SettingsModel(
            IThemeManager themeManager,
            IWindowManager windowManager)
            : this(
                themeManager,
                windowManager,
                null,
                null,
                null,
                null,
                null)
        {
        }

        public SettingsModel(
            IThemeManager themeManager,
            IWindowManager windowManager,
            IWorkspaceService? workspaceService,
            IFolderPickerService? folderPickerService,
            IFileLauncherService? fileLauncherService,
            IWorkspaceFileOpenService? workspaceFileOpenService = null,
            IKeyResetService? keyResetService = null)
        {
            this.themeManager = themeManager ??
                throw new ArgumentNullException(nameof(themeManager));
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
            this.workspaceService = workspaceService;
            this.folderPickerService = folderPickerService;
            this.fileLauncherService = fileLauncherService;
            this.workspaceFileOpenService = workspaceFileOpenService;
            this.keyResetService = keyResetService;

            selectedTheme = Themes.First(
                option => option.Theme == themeManager.CurrentTheme);
            selectedLanguage = Languages.First(
                option => option.CultureName ==
                    LocalizationManager.CurrentCultureName);
            navigationPaneWidth = new GridLength(
                NormalizeNavigationPaneWidth(
                    Properties.Settings.Default.SettingsNavigationPaneWidth));
            int storedMinutes = Properties.Settings.Default.KeyResetTimeoutMinutes;
            selectedKeyResetInterval = KeyResetIntervals.FirstOrDefault(
                option => option.Minutes == storedMinutes)
                ?? KeyResetIntervals.First(option => option.Minutes == 15);
        }

        public Guid WindowId { get; } = Guid.NewGuid();

        public IReadOnlyList<ApplicationThemeOption> Themes =>
            themeManager.AvailableThemes;

        public ApplicationThemeOption SelectedTheme
        {
            get => selectedTheme;
            set
            {
                if(value is null)
                    return;
                if(SetProperty(ref selectedTheme, value))
                    themeManager.ApplyTheme(value.Theme);
            }
        }

        public IReadOnlyList<ApplicationLanguageOption> Languages =>
            LocalizationManager.AvailableLanguages;

        public ApplicationLanguageOption SelectedLanguage
        {
            get => selectedLanguage;
            set
            {
                if(value is null)
                    return;
                if(!SetProperty(ref selectedLanguage, value))
                    return;

                LocalizationManager.SelectCulture(value.CultureName);
                selectedTheme = Themes.First(
                    option => option.Theme == themeManager.CurrentTheme);
                OnPropertyChanged(nameof(Themes));
                OnPropertyChanged(nameof(SelectedTheme));
                OnPropertyChanged(nameof(SelectedCultureName));
            }
        }

        public string SelectedCultureName
        {
            get => selectedLanguage.CultureName;
            set
            {
                if(string.IsNullOrWhiteSpace(value))
                    return;

                string normalized =
                    LocalizationManager.NormalizeCultureName(value);
                ApplicationLanguageOption language = Languages.First(
                    option => option.CultureName == normalized);
                SelectedLanguage = language;
            }
        }

        public GridLength NavigationPaneWidth
        {
            get => navigationPaneWidth;
            set
            {
                double width = value.IsAbsolute
                    ? value.Value
                    : navigationPaneWidth.Value;
                SetProperty(
                    ref navigationPaneWidth,
                    new GridLength(NormalizeNavigationPaneWidth(width)));
            }
        }

        public int SelectedSectionIndex
        {
            get => selectedSectionIndex;
            set => SetProperty(ref selectedSectionIndex, value);
        }

        public string WorkspaceDirectory =>
            workspaceService?.WorkspaceDirectory ?? string.Empty;

        public string SearchQuery
        {
            get => searchQuery;
            set => SetProperty(ref searchQuery, value);
        }

        public IReadOnlyList<WorkspaceSearchResult> SearchResults
        {
            get => searchResults;
            private set => SetProperty(ref searchResults, value);
        }

        public bool IsSearching
        {
            get => isSearching;
            private set => SetProperty(ref isSearching, value);
        }

        public string SearchStatus
        {
            get => searchStatus;
            private set => SetProperty(ref searchStatus, value);
        }

        public IReadOnlyList<KeyResetIntervalOption> KeyResetIntervals =>
            KeyResetIntervalOption.All;

        public KeyResetIntervalOption SelectedKeyResetInterval
        {
            get => selectedKeyResetInterval;
            set
            {
                if(value is null || !SetProperty(ref selectedKeyResetInterval, value))
                    return;
                Properties.Settings.Default.KeyResetTimeoutMinutes = value.Minutes;
                Properties.Settings.Default.Save();
                keyResetService?.UpdateTimeout(value.Minutes <= 0
                    ? TimeSpan.Zero
                    : TimeSpan.FromMinutes(value.Minutes));
            }
        }

        public async Task ChooseWorkspaceAsync()
        {
            if(workspaceService is null || folderPickerService is null)
                return;

            try
            {
                string? selectedPath =
                    await folderPickerService.PickFolderAsync(
                        WorkspaceDirectory,
                        CancellationToken.None);
                if(string.IsNullOrWhiteSpace(selectedPath))
                    return;

                workspaceService.SetWorkspaceDirectory(selectedPath);
                searchCancellation?.Cancel();
                IsSearching = false;
                SearchResults = Array.Empty<WorkspaceSearchResult>();
                SearchStatus = string.Empty;
                OnPropertyChanged(nameof(WorkspaceDirectory));
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                SearchStatus = LocalizationManager.Format(
                    "Settings.Workspace.ChooseFailed",
                    ex.Message);
            }
        }

        public async Task SearchAsync()
        {
            if(workspaceService is null)
                return;
            if(string.IsNullOrWhiteSpace(SearchQuery))
            {
                SearchResults = Array.Empty<WorkspaceSearchResult>();
                SearchStatus = LocalizationManager.GetString(
                    "Settings.Search.EnterQuery");
                return;
            }

            // Результат завершившегося старого запроса не должен заменить
            // результаты более нового поиска.
            searchCancellation?.Cancel();
            searchCancellation?.Dispose();
            searchCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = searchCancellation.Token;

            IsSearching = true;
            SearchStatus = LocalizationManager.GetString(
                "Settings.Search.InProgress");

            try
            {
                WorkspaceSearchOutcome outcome =
                    await workspaceService.SearchFilesAsync(
                        SearchQuery,
                        cancellationToken: cancellationToken);

                SearchResults = outcome.Results;
                SearchStatus = CreateSearchStatus(outcome);
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                SearchResults = Array.Empty<WorkspaceSearchResult>();
                SearchStatus = ex.Message;
            }
            finally
            {
                // Отменённая задача уже могла быть заменена новым поиском;
                // его индикатор выполнения здесь выключать нельзя.
                if(!cancellationToken.IsCancellationRequested)
                    IsSearching = false;
            }
        }

        public async Task OpenSearchResultAsync(
            WorkspaceSearchResult? result,
            CancellationToken cancellationToken = default)
        {
            if(result is null)
                return;

            string failureMessage = LocalizationManager.GetString(
                "Settings.Search.OpenFailed");

            try
            {
                if(workspaceFileOpenService is null)
                {
                    ExecuteLaunch(
                        result,
                        item => fileLauncherService?.Open(item.FullPath),
                        failureMessage);
                    return;
                }

                WorkspaceFileOpenResult openResult =
                    await workspaceFileOpenService.OpenAsync(
                        result.FullPath,
                        cancellationToken);
                if(openResult.Cancelled)
                    return;
                if(!openResult.Success)
                {
                    SearchStatus = $"{failureMessage}: {openResult.Error}";
                    return;
                }

                if(openResult.OpenedInternally)
                    windowManager.CloseWindow(WindowId);
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception ex)
            {
                SearchStatus = $"{failureMessage}: {ex.Message}";
            }
        }

        public void RevealSearchResult(WorkspaceSearchResult? result) =>
            ExecuteLaunch(
                result,
                item => fileLauncherService?.RevealInExplorer(item.FullPath),
                LocalizationManager.GetString("Settings.Search.RevealFailed"));

        public void OpenEncryptionKeyDialog()
        {
            Guid windowId = windowManager.CreateWindow<KeyInputWindow>();
            windowManager.ShowWindowDialog(windowId);
            keyResetService?.NotifyActivity();
        }

        public void Close() => windowManager.CloseWindow(WindowId);

        public void Closing()
        {
            searchCancellation?.Cancel();
            Properties.Settings.Default.SettingsNavigationPaneWidth =
                NavigationPaneWidth.Value;
            Properties.Settings.Default.Save();
        }

        public void Closed()
        {
            searchCancellation?.Dispose();
            searchCancellation = null;
        }

        private static string CreateSearchStatus(
            WorkspaceSearchOutcome outcome)
        {
            string status = outcome.Results.Count switch
            {
                0 => LocalizationManager.GetString(
                    "Settings.Search.NoFiles"),
                1 => LocalizationManager.GetString(
                    "Settings.Search.OneFile"),
                _ => LocalizationManager.Format(
                    "Settings.Search.ManyFiles",
                    outcome.Results.Count)
            };

            if(outcome.IsTruncated)
            {
                status += LocalizationManager.GetString(
                    "Settings.Search.Truncated");
            }
            if(outcome.SkippedDirectoryCount > 0)
            {
                status += LocalizationManager.Format(
                    "Settings.Search.SkippedDirectories",
                    outcome.SkippedDirectoryCount);
            }

            return status;
        }

        private static double NormalizeNavigationPaneWidth(double width) =>
            double.IsFinite(width)
                ? Math.Clamp(width, 150d, 340d)
                : 190d;

        private void ExecuteLaunch(
            WorkspaceSearchResult? result,
            Func<WorkspaceSearchResult, CryptoBook.DTO.LaunchResult?> launch,
            string failureMessage)
        {
            if(result is null || fileLauncherService is null)
                return;

            CryptoBook.DTO.LaunchResult? launchResult = launch(result);
            if(launchResult is { Success: false })
                SearchStatus = $"{failureMessage}: {launchResult.Value.Error}";
        }
    }
}
