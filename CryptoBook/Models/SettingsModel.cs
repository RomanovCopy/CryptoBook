using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Windows;

namespace CryptoBook.Models
{
    public sealed class SettingsModel:
        ViewModelBase,
        ISettingsModel
    {
        private readonly IThemeManager themeManager;
        private readonly IWindowManager windowManager;
        private readonly IWorkspaceService? workspaceService;
        private readonly IFolderPickerService? folderPickerService;
        private readonly IFileLauncherService? fileLauncherService;
        private ApplicationThemeOption selectedTheme;
        private GridLength navigationPaneWidth;
        private CancellationTokenSource? searchCancellation;
        private int selectedSectionIndex;
        private string searchQuery = string.Empty;
        private IReadOnlyList<WorkspaceSearchResult> searchResults =
            Array.Empty<WorkspaceSearchResult>();
        private bool isSearching;
        private string searchStatus = string.Empty;

        public SettingsModel(
            IThemeManager themeManager,
            IWindowManager windowManager)
            : this(
                themeManager,
                windowManager,
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
            IFileLauncherService? fileLauncherService)
        {
            this.themeManager = themeManager ??
                throw new ArgumentNullException(nameof(themeManager));
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
            this.workspaceService = workspaceService;
            this.folderPickerService = folderPickerService;
            this.fileLauncherService = fileLauncherService;

            selectedTheme = Themes.First(
                option => option.Theme == themeManager.CurrentTheme);
            navigationPaneWidth = new GridLength(
                NormalizeNavigationPaneWidth(
                    Properties.Settings.Default.SettingsNavigationPaneWidth));
        }

        public Guid WindowId { get; } = Guid.NewGuid();

        public IReadOnlyList<ApplicationThemeOption> Themes =>
            themeManager.AvailableThemes;

        public ApplicationThemeOption SelectedTheme
        {
            get => selectedTheme;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if(SetProperty(ref selectedTheme, value))
                    themeManager.ApplyTheme(value.Theme);
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
                SearchStatus =
                    $"Не удалось выбрать рабочую директорию: {ex.Message}";
            }
        }

        public async Task SearchAsync()
        {
            if(workspaceService is null)
                return;
            if(string.IsNullOrWhiteSpace(SearchQuery))
            {
                SearchResults = Array.Empty<WorkspaceSearchResult>();
                SearchStatus = "Введите часть имени файла.";
                return;
            }

            searchCancellation?.Cancel();
            searchCancellation?.Dispose();
            searchCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = searchCancellation.Token;

            IsSearching = true;
            SearchStatus = "Поиск...";

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
                if(!cancellationToken.IsCancellationRequested)
                    IsSearching = false;
            }
        }

        public void OpenSearchResult(WorkspaceSearchResult? result) =>
            ExecuteLaunch(
                result,
                item => fileLauncherService?.Open(item.FullPath),
                "Не удалось открыть файл");

        public void RevealSearchResult(WorkspaceSearchResult? result) =>
            ExecuteLaunch(
                result,
                item => fileLauncherService?.RevealInExplorer(item.FullPath),
                "Не удалось показать файл");

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
                0 => "Файлы не найдены.",
                1 => "Найден 1 файл.",
                _ => $"Найдено файлов: {outcome.Results.Count}."
            };

            if(outcome.IsTruncated)
                status += " Показаны первые 200 результатов.";
            if(outcome.SkippedDirectoryCount > 0)
            {
                status +=
                    $" Пропущено недоступных папок: {outcome.SkippedDirectoryCount}.";
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
