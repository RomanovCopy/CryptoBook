using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Models
{
    public sealed class WorkspaceSearchModel: ViewModelBase
    {
        private readonly IWorkspaceContentSearchService searchService;
        private readonly IWorkspaceFileOpenService fileOpenService;
        private readonly IWorkspaceDocumentDeleteService deleteService;
        private readonly IPageNavigationService navigationService;
        private IReadOnlyList<WorkspaceContentSearchResult> searchResults =
            Array.Empty<WorkspaceContentSearchResult>();
        private string searchQuery = string.Empty;
        private string searchStatus = string.Empty;
        private string currentFile = string.Empty;
        private bool isSearching;

        public WorkspaceSearchModel(
            IWorkspaceContentSearchService searchService,
            IWorkspaceFileOpenService fileOpenService,
            IWorkspaceDocumentDeleteService deleteService,
            IPageNavigationService navigationService)
        {
            this.searchService = searchService ??
                throw new ArgumentNullException(nameof(searchService));
            this.fileOpenService = fileOpenService ??
                throw new ArgumentNullException(nameof(fileOpenService));
            this.deleteService = deleteService ??
                throw new ArgumentNullException(nameof(deleteService));
            this.navigationService = navigationService ??
                throw new ArgumentNullException(nameof(navigationService));
        }

        public string SearchQuery
        {
            get => searchQuery;
            set => SetProperty(ref searchQuery, value);
        }

        public string SearchStatus
        {
            get => searchStatus;
            private set => SetProperty(ref searchStatus, value);
        }

        public string CurrentFile
        {
            get => currentFile;
            private set => SetProperty(ref currentFile, value);
        }

        public bool IsSearching
        {
            get => isSearching;
            private set => SetProperty(ref isSearching, value);
        }

        public IReadOnlyList<WorkspaceContentSearchResult> SearchResults
        {
            get => searchResults;
            private set => SetProperty(ref searchResults, value);
        }

        public bool CanSearch() =>
            !IsSearching && !string.IsNullOrWhiteSpace(SearchQuery);

        public async Task SearchAsync(CancellationToken cancellationToken)
        {
            if(!CanSearch())
                return;

            IsSearching = true;
            SearchResults = Array.Empty<WorkspaceContentSearchResult>();
            SearchStatus = LocalizationManager.GetString(
                "Workspace.ContentSearch.InProgress");
            CurrentFile = string.Empty;

            var progress = new CallbackProgress<WorkspaceContentSearchProgress>(item =>
            {
                CurrentFile = item.CurrentRelativePath;
                SearchStatus = LocalizationManager.Format(
                    "Workspace.ContentSearch.FilesProcessed",
                    item.ProcessedFileCount);
            });

            try
            {
                WorkspaceContentSearchOutcome outcome =
                    await searchService.SearchAsync(
                        SearchQuery,
                        progress,
                        cancellationToken);
                SearchResults = outcome.Results;
                SearchStatus = CreateStatus(outcome);
            }
            catch(OperationCanceledException)
            {
                SearchStatus = LocalizationManager.GetString(
                    "Workspace.ContentSearch.Cancelled");
            }
            catch(Exception exception)
            {
                SearchStatus = exception.Message;
            }
            finally
            {
                CurrentFile = string.Empty;
                IsSearching = false;
            }
        }

        public async Task OpenResultAsync(
            WorkspaceContentSearchResult? result,
            CancellationToken cancellationToken)
        {
            if(result is null)
                return;

            try
            {
                WorkspaceFileOpenResult openResult =
                    await fileOpenService.OpenAsync(
                        result.FullPath,
                        cancellationToken);
                if(openResult.Cancelled)
                    return;
                if(!openResult.Success)
                {
                    SearchStatus = LocalizationManager.Format(
                        "Workspace.ContentSearch.OpenFailed",
                        openResult.Error ?? string.Empty);
                    return;
                }

                if(openResult.OpenedInternally)
                    navigationService.Navigate("Home");
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception exception)
            {
                SearchStatus = LocalizationManager.Format(
                    "Workspace.ContentSearch.OpenFailed",
                    exception.Message);
            }
        }

        public async Task DeleteResultAsync(
            WorkspaceContentSearchResult? result,
            CancellationToken cancellationToken)
        {
            if(result is null || IsSearching)
                return;

            try
            {
                WorkspaceDocumentDeleteResult deleteResult =
                    await deleteService.DeleteAsync(
                        result,
                        cancellationToken);
                if(deleteResult.Cancelled)
                    return;
                if(!deleteResult.Deleted)
                {
                    SearchStatus = LocalizationManager.Format(
                        "Workspace.ContentSearch.DeleteError",
                        deleteResult.Error ?? string.Empty);
                    return;
                }

                SearchResults = SearchResults
                    .Where(item => !string.Equals(
                        item.FullPath,
                        result.FullPath,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                SearchStatus = LocalizationManager.Format(
                    "Workspace.ContentSearch.DeleteSucceeded",
                    result.Name);
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception exception)
            {
                SearchStatus = LocalizationManager.Format(
                    "Workspace.ContentSearch.DeleteError",
                    exception.Message);
            }
        }

        public void Clear()
        {
            SearchResults = Array.Empty<WorkspaceContentSearchResult>();
            SearchStatus = string.Empty;
            CurrentFile = string.Empty;
        }

        public void Close()
        {
            Clear();
            navigationService.Navigate("Home");
            navigationService.Remove("WorkspaceSearch");
        }

        private static string CreateStatus(
            WorkspaceContentSearchOutcome outcome)
        {
            string status = LocalizationManager.Format(
                "Workspace.ContentSearch.Results",
                outcome.Results.Count);
            if(outcome.IsTruncated)
            {
                status += LocalizationManager.GetString(
                    "Workspace.ContentSearch.Truncated");
            }
            if(outcome.SkippedDirectoryCount > 0)
            {
                status += LocalizationManager.Format(
                    "Workspace.ContentSearch.SkippedDirectories",
                    outcome.SkippedDirectoryCount);
            }
            if(outcome.SkippedFileCount > 0)
            {
                status += LocalizationManager.Format(
                    "Workspace.ContentSearch.SkippedFiles",
                    outcome.SkippedFileCount);
            }
            if(outcome.SkippedEncryptedFileCount > 0)
            {
                status += LocalizationManager.Format(
                    "Workspace.ContentSearch.SkippedEncrypted",
                    outcome.SkippedEncryptedFileCount);
            }
            return status;
        }

        private sealed class CallbackProgress<T>(Action<T> callback):
            IProgress<T>
        {
            private readonly Action<T> callbackAction = callback ??
                throw new ArgumentNullException(nameof(callback));

            public void Report(T value) => callbackAction(value);
        }
    }
}
