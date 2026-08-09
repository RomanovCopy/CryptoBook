using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    /// <summary>
    /// Представляет Quick Access и направляет открытие только через общий
    /// координатор безопасного переключения документов.
    /// </summary>
    public sealed class PinnedDocumentsViewModel:
        ViewModelBase,
        IPinnedDocumentsViewModel
    {
        private readonly IPinnedDocumentService service;
        private readonly IDocumentSwitchCoordinator switchCoordinator;
        private readonly IDocumentSession documentSession;
        private readonly IMessageService messageService;
        private readonly ICurrentDocumentSaver currentDocumentSaver;
        private readonly IFileLauncherService fileLauncherService;
        private readonly IFilePickerService filePickerService;
        private readonly ObservableCollection<PinnedDocumentItemViewModel> items = [];
        private readonly AsyncRelayCommand toggleCurrentCommand;
        private readonly AsyncRelayCommand openCommand;
        private readonly AsyncRelayCommand unpinCommand;
        private readonly AsyncRelayCommand revealCommand;
        private readonly AsyncRelayCommand relocateCommand;
        private readonly AsyncRelayCommand moveUpCommand;
        private readonly AsyncRelayCommand moveDownCommand;
        private readonly RelayCommand refreshAvailabilityCommand;
        private readonly SemaphoreSlim initializationGate = new(1, 1);
        private bool initialized;
        private bool disposed;

        public PinnedDocumentsViewModel(
            IPinnedDocumentService service,
            IDocumentSwitchCoordinator switchCoordinator,
            IDocumentSession documentSession,
            IMessageService messageService,
            ICurrentDocumentSaver currentDocumentSaver,
            IFileLauncherService fileLauncherService,
            IFilePickerService filePickerService)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.switchCoordinator = switchCoordinator
                ?? throw new ArgumentNullException(nameof(switchCoordinator));
            this.documentSession = documentSession
                ?? throw new ArgumentNullException(nameof(documentSession));
            this.messageService = messageService
                ?? throw new ArgumentNullException(nameof(messageService));
            this.currentDocumentSaver = currentDocumentSaver
                ?? throw new ArgumentNullException(nameof(currentDocumentSaver));
            this.fileLauncherService = fileLauncherService
                ?? throw new ArgumentNullException(nameof(fileLauncherService));
            this.filePickerService = filePickerService
                ?? throw new ArgumentNullException(nameof(filePickerService));

            Items = new ReadOnlyObservableCollection<PinnedDocumentItemViewModel>(
                items);
            toggleCurrentCommand = new AsyncRelayCommand(
                ToggleCurrentAsync,
                _ => documentSession.HasDocument);
            openCommand = new AsyncRelayCommand(
                OpenAsync,
                parameter => parameter is PinnedDocumentItemViewModel
                {
                    IsOpening: false
                });
            unpinCommand = new AsyncRelayCommand(
                UnpinAsync,
                parameter => parameter is PinnedDocumentItemViewModel);
            revealCommand = new AsyncRelayCommand(
                RevealAsync,
                parameter => parameter is PinnedDocumentItemViewModel);
            relocateCommand = new AsyncRelayCommand(
                RelocateAsync,
                parameter => parameter is PinnedDocumentItemViewModel
                {
                    IsMissing: true
                });
            moveUpCommand = new AsyncRelayCommand(
                (parameter, token) => MoveAsync(parameter, -1, token),
                parameter => parameter is PinnedDocumentItemViewModel
                {
                    CanMoveUp: true
                });
            moveDownCommand = new AsyncRelayCommand(
                (parameter, token) => MoveAsync(parameter, 1, token),
                parameter => parameter is PinnedDocumentItemViewModel
                {
                    CanMoveDown: true
                });
            refreshAvailabilityCommand = new RelayCommand(
                RefreshAvailability,
                parameter => parameter is PinnedDocumentItemViewModel);

            documentSession.PropertyChanged += OnDocumentSessionPropertyChanged;
            LocalizationManager.CultureChanged += OnCultureChanged;
        }

        public ReadOnlyObservableCollection<PinnedDocumentItemViewModel> Items { get; }

        public bool HasItems => Items.Count > 0;

        public bool IsCurrentDocumentPinned =>
            documentSession.FilePath is { Length: > 0 } path &&
            service.IsPinned(path);

        public string CurrentPinGlyph => IsCurrentDocumentPinned
            ? "\uE77A"
            : "\uE840";

        public string CurrentPinToolTip => LocalizationManager.GetString(
            IsCurrentDocumentPinned
                ? "PinnedDocuments.UnpinCurrent"
                : string.IsNullOrWhiteSpace(documentSession.FilePath)
                    ? "PinnedDocuments.SaveAndPinCurrent"
                    : "PinnedDocuments.PinCurrent");

        public ICommand ToggleCurrentCommand => toggleCurrentCommand;
        public ICommand OpenCommand => openCommand;
        public ICommand UnpinCommand => unpinCommand;
        public ICommand RevealCommand => revealCommand;
        public ICommand RelocateCommand => relocateCommand;
        public ICommand MoveUpCommand => moveUpCommand;
        public ICommand MoveDownCommand => moveDownCommand;
        public ICommand RefreshAvailabilityCommand => refreshAvailabilityCommand;

        public async Task InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            await initializationGate.WaitAsync(cancellationToken);
            try
            {
                if(initialized)
                    return;

                await service.InitializeAsync(cancellationToken);
                if(disposed)
                    return;

                service.Changed += OnServiceChanged;
                initialized = true;
                Rebuild();
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception exception)
            {
                await ShowErrorAsync("PinnedDocuments.LoadFailed", exception);
            }
            finally
            {
                initializationGate.Release();
            }
        }

        private async Task ToggleCurrentAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            string? path = documentSession.FilePath;
            if(string.IsNullOrWhiteSpace(path) && documentSession.HasDocument)
            {
                bool saved = await currentDocumentSaver.TrySaveCurrentAsync(
                    cancellationToken);
                if(!saved)
                    return;

                path = documentSession.FilePath;
            }

            if(string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if(service.IsPinned(path))
                    await service.UnpinAsync(path, cancellationToken);
                else
                    await service.PinAsync(path, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception exception)
            {
                await ShowErrorAsync("PinnedDocuments.SaveFailed", exception);
            }
        }

        private async Task OpenAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            if(parameter is not PinnedDocumentItemViewModel item)
                return;

            item.RefreshAvailability();
            if(item.IsMissing)
            {
                RaiseCommandStates();
                await messageService.ShowMessage(
                    LocalizationManager.GetString("PinnedDocuments.ErrorTitle"),
                    LocalizationManager.Format(
                        "PinnedDocuments.MissingFile",
                        item.FileName));
                return;
            }

            item.IsOpening = true;
            openCommand.RaiseCanExecuteChanged();
            try
            {
                WorkspaceFileOpenResult result = await switchCoordinator.SwitchAsync(
                    item.Path,
                    cancellationToken);
                if(result.Cancelled)
                    return;

                if(!result.Success)
                {
                    Debug.WriteLine(result.Error);
                    await messageService.ShowMessage(
                        LocalizationManager.GetString("PinnedDocuments.ErrorTitle"),
                        LocalizationManager.Format(
                            "PinnedDocuments.OpenFailed",
                            item.FileName));
                    return;
                }

                item.IsOpening = false;
                await service.MarkOpenedAsync(item.Path, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception exception)
            {
                Debug.WriteLine(exception);
                await messageService.ShowMessage(
                    LocalizationManager.GetString("PinnedDocuments.ErrorTitle"),
                    LocalizationManager.Format(
                        "PinnedDocuments.OpenFailed",
                        item.FileName));
            }
            finally
            {
                item.IsOpening = false;
                openCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task UnpinAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            if(parameter is not PinnedDocumentItemViewModel item)
                return;

            try
            {
                await service.UnpinAsync(item.Path, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception exception)
            {
                await ShowErrorAsync("PinnedDocuments.SaveFailed", exception);
            }
        }

        private async Task RevealAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            if(parameter is not PinnedDocumentItemViewModel item)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            item.RefreshAvailability();
            string target = item.IsAvailable
                ? item.Path
                : item.ParentDirectory;
            LaunchResult result = fileLauncherService.RevealInExplorer(
                target,
                select: item.IsAvailable);
            if(result.Success)
                return;

            Debug.WriteLine(result.Error);
            await messageService.ShowMessage(
                LocalizationManager.GetString("PinnedDocuments.ErrorTitle"),
                LocalizationManager.Format(
                    "PinnedDocuments.RevealFailed",
                    item.FileName));
        }

        private async Task RelocateAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            if(parameter is not PinnedDocumentItemViewModel item)
                return;

            item.RefreshAvailability();
            if(item.IsAvailable)
            {
                RaiseCommandStates();
                return;
            }

            string? selectedPath = await filePickerService.PickFileAsync(
                item.ParentDirectory,
                cancellationToken);
            if(string.IsNullOrWhiteSpace(selectedPath))
                return;

            selectedPath = GetNativePath(selectedPath);
            if(!File.Exists(selectedPath))
            {
                await messageService.ShowMessage(
                    LocalizationManager.GetString("PinnedDocuments.ErrorTitle"),
                    LocalizationManager.Format(
                        "PinnedDocuments.RelocateInvalid",
                        item.FileName));
                return;
            }

            try
            {
                await service.UpdatePathAsync(
                    item.Path,
                    selectedPath,
                    cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception exception)
            {
                await ShowErrorAsync("PinnedDocuments.RelocateFailed", exception);
            }
        }

        private async Task MoveAsync(
            object? parameter,
            int offset,
            CancellationToken cancellationToken)
        {
            if(parameter is not PinnedDocumentItemViewModel item)
                return;

            try
            {
                await service.MoveAsync(item.Path, offset, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception exception)
            {
                await ShowErrorAsync("PinnedDocuments.SaveFailed", exception);
            }
        }

        private void RefreshAvailability(object? parameter)
        {
            if(parameter is not PinnedDocumentItemViewModel item)
                return;

            item.RefreshAvailability();
            RaiseCommandStates();
        }

        private void Rebuild()
        {
            items.Clear();
            PinnedDocument[] documents = service.Items
                .OrderBy(item => item.SortOrder)
                .ToArray();
            foreach(PinnedDocument document in documents)
            {
                var item = new PinnedDocumentItemViewModel(document);
                item.UpdateOrdering(items.Count, documents.Length);
                item.UpdateDocumentState(
                    documentSession.FilePath,
                    documentSession.IsDirty);
                items.Add(item);
            }

            OnPropertyChanged(
                nameof(HasItems),
                nameof(IsCurrentDocumentPinned),
                nameof(CurrentPinGlyph),
                nameof(CurrentPinToolTip));
            RaiseCommandStates();
        }

        private void UpdateDocumentState()
        {
            foreach(PinnedDocumentItemViewModel item in items)
            {
                item.UpdateDocumentState(
                    documentSession.FilePath,
                    documentSession.IsDirty);
            }

            OnPropertyChanged(
                nameof(IsCurrentDocumentPinned),
                nameof(CurrentPinGlyph),
                nameof(CurrentPinToolTip));
            RaiseCommandStates();
        }

        private void RaiseCommandStates()
        {
            toggleCurrentCommand.RaiseCanExecuteChanged();
            openCommand.RaiseCanExecuteChanged();
            unpinCommand.RaiseCanExecuteChanged();
            revealCommand.RaiseCanExecuteChanged();
            relocateCommand.RaiseCanExecuteChanged();
            moveUpCommand.RaiseCanExecuteChanged();
            moveDownCommand.RaiseCanExecuteChanged();
        }

        private async Task ShowErrorAsync(string resourceKey, Exception exception)
        {
            Debug.WriteLine(exception);
            await messageService.ShowMessage(
                LocalizationManager.GetString("PinnedDocuments.ErrorTitle"),
                LocalizationManager.GetString(resourceKey));
        }

        private static string GetNativePath(string path)
        {
            const string localPrefix = "local://";
            string trimmed = path.Trim();
            return trimmed.StartsWith(
                localPrefix,
                StringComparison.OrdinalIgnoreCase)
                ? trimmed[localPrefix.Length..]
                : trimmed;
        }

        private void OnServiceChanged(object? sender, EventArgs args) => Rebuild();

        private void OnDocumentSessionPropertyChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName is nameof(IDocumentSession.FilePath) or
               nameof(IDocumentSession.IsDirty) or
               nameof(IDocumentSession.HasDocument) or
               nameof(IDocumentSession.DisplayName))
            {
                UpdateDocumentState();
            }
        }

        private void OnCultureChanged(object? sender, EventArgs args) =>
            OnPropertyChanged(nameof(CurrentPinToolTip));

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            service.Changed -= OnServiceChanged;
            documentSession.PropertyChanged -= OnDocumentSessionPropertyChanged;
            LocalizationManager.CultureChanged -= OnCultureChanged;
        }
    }
}
