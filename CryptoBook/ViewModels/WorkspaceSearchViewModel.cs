using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class WorkspaceSearchViewModel:
        ViewModelBase,
        IWorkspaceSearchViewModel
    {
        private readonly WorkspaceSearchModel model;
        private readonly AsyncRelayCommand searchCommand;
        private readonly AsyncRelayCommand openResultCommand;
        private readonly AsyncRelayCommand deleteResultCommand;
        private readonly RelayCommand cancelSearchCommand;
        private readonly RelayCommand pageLoadedCommand;
        private readonly RelayCommand pageClearCommand;
        private readonly RelayCommand closePageCommand;

        public WorkspaceSearchViewModel(WorkspaceSearchModel model)
        {
            this.model = model ??
                throw new ArgumentNullException(nameof(model));
            searchCommand = new AsyncRelayCommand(
                (_, token) => model.SearchAsync(token),
                _ => model.CanSearch());
            openResultCommand = new AsyncRelayCommand(
                (parameter, token) => model.OpenResultAsync(
                    parameter as WorkspaceContentSearchResult,
                    token),
                parameter => parameter is WorkspaceContentSearchResult &&
                    !model.IsSearching);
            deleteResultCommand = new AsyncRelayCommand(
                (parameter, token) => model.DeleteResultAsync(
                    parameter as WorkspaceContentSearchResult,
                    token),
                parameter => parameter is WorkspaceContentSearchResult &&
                    !model.IsSearching);
            cancelSearchCommand = new RelayCommand(
                _ => searchCommand.Cancel(),
                _ => model.IsSearching);
            pageLoadedCommand = new RelayCommand(_ => { });
            pageClearCommand = new RelayCommand(
                _ => model.Clear(),
                _ => !model.IsSearching);
            closePageCommand = new RelayCommand(_ =>
            {
                searchCommand.Cancel();
                model.Close();
            });

            model.PropertyChanged += (_, args) =>
            {
                OnPropertyChanged(args.PropertyName ?? string.Empty);
                if(args.PropertyName is nameof(model.IsSearching) or
                   nameof(model.SearchQuery))
                {
                    searchCommand.RaiseCanExecuteChanged();
                    openResultCommand.RaiseCanExecuteChanged();
                    deleteResultCommand.RaiseCanExecuteChanged();
                    cancelSearchCommand.RaiseCanExecuteChanged();
                    pageClearCommand.RaiseCanExecuteChanged();
                }
            };
        }

        public string SearchQuery
        {
            get => model.SearchQuery;
            set => model.SearchQuery = value;
        }

        public string SearchStatus => model.SearchStatus;
        public string CurrentFile => model.CurrentFile;
        public bool IsSearching => model.IsSearching;
        public IReadOnlyList<WorkspaceContentSearchResult> SearchResults =>
            model.SearchResults;

        public ICommand Search => searchCommand;
        public ICommand CancelSearch => cancelSearchCommand;
        public ICommand OpenResult => openResultCommand;
        public ICommand DeleteResult => deleteResultCommand;
        public ICommand ClearPage => pageClearCommand;
        public ICommand ClosePage => closePageCommand;
        public ICommand PageLoaded => pageLoadedCommand;
        public ICommand PageClear => pageClearCommand;
        public ICommand Loaded => PageLoaded;
        public ICommand Close => ClosePage;
        public ICommand Closing => cancelSearchCommand;
        public ICommand Closed => pageLoadedCommand;
    }
}
