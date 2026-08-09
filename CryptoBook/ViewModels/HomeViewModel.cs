using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    /// <summary>
    /// Переключает Home между стартовым экраном и редактором и направляет
    /// все операции открытия в общий координатор документов.
    /// </summary>
    public sealed class HomeViewModel: ViewModelBase, IHomeViewModel
    {
        private readonly IDocumentSession documentSession;
        private readonly IMenuFileViewModel menuFileViewModel;
        private readonly IMenuContentViewModel menuContentViewModel;
        private readonly IFolderPickerService folderPickerService;
        private readonly IWorkspaceService workspaceService;
        private readonly IMessageService messageService;
        private readonly AsyncRelayCommand pageLoadedCommand;
        private readonly AsyncRelayCommand chooseWorkspaceCommand;
        private readonly RelayCommand noOperationCommand = new(_ => { });

        public HomeViewModel(
            IRichtextboxViewModel documentView,
            IDocumentSession documentSession,
            IMenuFileViewModel menuFileViewModel,
            IMenuContentViewModel menuContentViewModel,
            IRecentDocumentsViewModel recentDocuments,
            IPinnedDocumentsViewModel pinnedDocuments,
            IFolderPickerService folderPickerService,
            IWorkspaceService workspaceService,
            IMessageService messageService)
        {
            DocumentView = documentView
                ?? throw new ArgumentNullException(nameof(documentView));
            this.documentSession = documentSession
                ?? throw new ArgumentNullException(nameof(documentSession));
            this.menuFileViewModel = menuFileViewModel
                ?? throw new ArgumentNullException(nameof(menuFileViewModel));
            this.menuContentViewModel = menuContentViewModel
                ?? throw new ArgumentNullException(nameof(menuContentViewModel));
            RecentDocuments = recentDocuments
                ?? throw new ArgumentNullException(nameof(recentDocuments));
            PinnedDocuments = pinnedDocuments
                ?? throw new ArgumentNullException(nameof(pinnedDocuments));
            this.folderPickerService = folderPickerService
                ?? throw new ArgumentNullException(nameof(folderPickerService));
            this.workspaceService = workspaceService
                ?? throw new ArgumentNullException(nameof(workspaceService));
            this.messageService = messageService
                ?? throw new ArgumentNullException(nameof(messageService));

            pageLoadedCommand = new AsyncRelayCommand(InitializeAsync);
            chooseWorkspaceCommand = new AsyncRelayCommand(ChooseWorkspaceAsync);

            documentSession.PropertyChanged += OnDocumentSessionPropertyChanged;
            LocalizationManager.CultureChanged += OnCultureChanged;
        }

        public Action<object> BehaviorReady { get; set; } = _ => { };
        public IRichtextboxViewModel DocumentView { get; }
        public IRecentDocumentsViewModel RecentDocuments { get; }
        public IPinnedDocumentsViewModel PinnedDocuments { get; }

        public bool HasDocument => documentSession.HasDocument;

        public string WorkspaceDirectoryDisplay
        {
            get
            {
                try
                {
                    string path = workspaceService.WorkspaceDirectory;
                    return string.IsNullOrWhiteSpace(path)
                        ? LocalizationManager.GetString("Home.WorkspaceNotSelected")
                        : path;
                }
                catch(Exception exception)
                {
                    Debug.WriteLine(exception);
                    return LocalizationManager.GetString(
                        "Home.WorkspaceNotSelected");
                }
            }
        }

        public ICommand NewDocument => menuFileViewModel.NewFile;
        public ICommand OpenDocument => menuFileViewModel.OpenFile;
        public ICommand OpenMediaPlayer => menuContentViewModel.MediaPlayer;
        public ICommand ChooseWorkspace => chooseWorkspaceCommand;

        public ICommand PageLoaded => pageLoadedCommand;
        public ICommand PageClear => noOperationCommand;
        public ICommand Loaded => pageLoadedCommand;
        public ICommand Close => noOperationCommand;
        public ICommand Closing => noOperationCommand;
        public ICommand Closed => noOperationCommand;

        private async Task InitializeAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            await RecentDocuments.InitializeAsync(cancellationToken);
            await PinnedDocuments.InitializeAsync(cancellationToken);
        }

        private async Task ChooseWorkspaceAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            string? selectedPath = await folderPickerService.PickFolderAsync(
                GetWorkspaceDirectoryOrNull(),
                cancellationToken);
            if(string.IsNullOrWhiteSpace(selectedPath))
                return;

            try
            {
                workspaceService.SetWorkspaceDirectory(selectedPath);
                OnPropertyChanged(nameof(WorkspaceDirectoryDisplay));
            }
            catch(Exception exception) when(
                exception is IOException or
                ArgumentException or
                InvalidOperationException or
                NotSupportedException)
            {
                Debug.WriteLine(exception);
                await messageService.ShowMessage(
                    LocalizationManager.GetString("Home.WorkspaceErrorTitle"),
                    exception.Message);
            }
        }

        private string? GetWorkspaceDirectoryOrNull()
        {
            try
            {
                string path = workspaceService.WorkspaceDirectory;
                return string.IsNullOrWhiteSpace(path) ? null : path;
            }
            catch(Exception exception)
            {
                Debug.WriteLine(exception);
                return null;
            }
        }

        private void OnDocumentSessionPropertyChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName == nameof(IDocumentSession.HasDocument))
                OnPropertyChanged(nameof(HasDocument));
        }

        private void OnCultureChanged(object? sender, EventArgs args) =>
            OnPropertyChanged(nameof(WorkspaceDirectoryDisplay));
    }
}
