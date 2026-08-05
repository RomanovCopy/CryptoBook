using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.ViewModels;

using System.ComponentModel;
using System.Windows.Controls;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class WorkspaceSearchModelTests
    {
        [Fact]
        public async Task DeleteResultAsync_AfterSuccessfulDeletion_RemovesResult()
        {
            WorkspaceContentSearchResult result = CreateResult();
            var deleteService = new DeleteServiceStub(
                WorkspaceDocumentDeleteResult.Success());
            var model = new WorkspaceSearchModel(
                new SearchServiceStub(result),
                new FileOpenServiceStub(),
                deleteService,
                new NavigationServiceStub());
            model.SearchQuery = "needle";
            await model.SearchAsync(CancellationToken.None);

            await model.DeleteResultAsync(result, CancellationToken.None);

            Assert.Same(result, deleteService.RequestedDocument);
            Assert.Empty(model.SearchResults);
        }

        [Fact]
        public async Task DeleteResultAsync_WhenDeletionCancelled_KeepsResult()
        {
            WorkspaceContentSearchResult result = CreateResult();
            var model = new WorkspaceSearchModel(
                new SearchServiceStub(result),
                new FileOpenServiceStub(),
                new DeleteServiceStub(
                    WorkspaceDocumentDeleteResult.Cancel()),
                new NavigationServiceStub());
            model.SearchQuery = "needle";
            await model.SearchAsync(CancellationToken.None);

            await model.DeleteResultAsync(result, CancellationToken.None);

            Assert.Single(model.SearchResults);
        }

        [Fact]
        public async Task Close_ClearsStateAndRemovesSearchPage()
        {
            WorkspaceContentSearchResult result = CreateResult();
            var navigation = new NavigationServiceStub();
            var model = new WorkspaceSearchModel(
                new SearchServiceStub(result),
                new FileOpenServiceStub(),
                new DeleteServiceStub(
                    WorkspaceDocumentDeleteResult.Success()),
                navigation);
            model.SearchQuery = "needle";
            await model.SearchAsync(CancellationToken.None);

            model.Close();

            Assert.Empty(model.SearchResults);
            Assert.Equal("Home", navigation.NavigatedKey);
            Assert.Equal("WorkspaceSearch", navigation.RemovedKey);
            Assert.Equal(["Navigate:Home", "Remove:WorkspaceSearch"],
                navigation.Operations);
        }

        [Fact]
        public void ClosePageCommand_WithoutSearch_IsEnabledAndRemovesPage()
        {
            var navigation = new NavigationServiceStub();
            var model = new WorkspaceSearchModel(
                new SearchServiceStub(CreateResult()),
                new FileOpenServiceStub(),
                new DeleteServiceStub(
                    WorkspaceDocumentDeleteResult.Success()),
                navigation);
            var viewModel = new WorkspaceSearchViewModel(model);

            Assert.True(viewModel.ClosePage.CanExecute(null));
            viewModel.ClosePage.Execute(null);

            Assert.Equal("Home", navigation.NavigatedKey);
            Assert.Equal("WorkspaceSearch", navigation.RemovedKey);
        }

        [Fact]
        public async Task OpenResultAsync_WhenOpenedInternally_NavigatesToEditor()
        {
            WorkspaceContentSearchResult result = CreateResult();
            var navigation = new NavigationServiceStub();
            var model = new WorkspaceSearchModel(
                new SearchServiceStub(result),
                new FileOpenServiceStub(
                    WorkspaceFileOpenResult.InternalSuccess()),
                new DeleteServiceStub(
                    WorkspaceDocumentDeleteResult.Success()),
                navigation);

            await model.OpenResultAsync(result, CancellationToken.None);

            Assert.Equal("Home", navigation.NavigatedKey);
        }

        private static WorkspaceContentSearchResult CreateResult() =>
            new(
                "notes.txt",
                @"C:\Workspace\notes.txt",
                "notes.txt",
                "a needle here",
                1,
                false);

        private sealed class SearchServiceStub(
            WorkspaceContentSearchResult result):
            IWorkspaceContentSearchService
        {
            public Task<WorkspaceContentSearchOutcome> SearchAsync(
                string query,
                IProgress<WorkspaceContentSearchProgress>? progress = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(new WorkspaceContentSearchOutcome(
                    [result],
                    false,
                    0,
                    0,
                    0));
        }

        private sealed class DeleteServiceStub(
            WorkspaceDocumentDeleteResult result):
            IWorkspaceDocumentDeleteService
        {
            public WorkspaceContentSearchResult? RequestedDocument
            {
                get;
                private set;
            }

            public Task<WorkspaceDocumentDeleteResult> DeleteAsync(
                WorkspaceContentSearchResult document,
                CancellationToken cancellationToken = default)
            {
                RequestedDocument = document;
                return Task.FromResult(result);
            }
        }

        private sealed class FileOpenServiceStub(
            WorkspaceFileOpenResult? result = null):
            IWorkspaceFileOpenService
        {
            public Task<WorkspaceFileOpenResult> OpenAsync(
                string filePath,
                CancellationToken cancellationToken = default) =>
                result is null
                    ? throw new NotSupportedException()
                    : Task.FromResult(result.Value);
        }

        private sealed class NavigationServiceStub: IPageNavigationService
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            public Page? CurrentPage => null;
            public string? CurrentKey => "WorkspaceSearch";
            public bool CanGoBack => false;
            public bool CanGoForward => false;
            public IReadOnlyList<string>? Keys => ["WorkspaceSearch"];
            public string? RemovedKey { get; private set; }
            public string? NavigatedKey { get; private set; }
            public List<string> Operations { get; } = [];

            public void Navigate(string key, object? args = null)
            {
                NavigatedKey = key;
                Operations.Add($"Navigate:{key}");
            }
            public void GoBack()
            {
            }
            public void GoForward()
            {
            }
            public void Remove(string key)
            {
                RemovedKey = key;
                Operations.Add($"Remove:{key}");
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(Keys)));
            }
        }
    }
}
