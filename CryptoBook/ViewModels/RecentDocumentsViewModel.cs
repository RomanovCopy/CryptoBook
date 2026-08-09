using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class RecentDocumentsViewModel:
        ViewModelBase,
        IRecentDocumentsViewModel
    {
        private readonly IRecentDocumentService service;
        private readonly IWorkspaceFileOpenService fileOpenService;
        private readonly IFilePickerService filePickerService;
        private readonly IMessageService messageService;
        private readonly ObservableCollection<RecentDocumentItemViewModel> items = [];
        private readonly AsyncRelayCommand openCommand;
        private readonly AsyncRelayCommand removeCommand;
        private readonly AsyncRelayCommand relocateCommand;
        private readonly RelayCommand refreshAvailabilityCommand;
        private bool initialized;
        private bool disposed;

        public RecentDocumentsViewModel(
            IRecentDocumentService service,
            IWorkspaceFileOpenService fileOpenService,
            IFilePickerService filePickerService,
            IMessageService messageService)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.fileOpenService = fileOpenService
                ?? throw new ArgumentNullException(nameof(fileOpenService));
            this.filePickerService = filePickerService
                ?? throw new ArgumentNullException(nameof(filePickerService));
            this.messageService = messageService
                ?? throw new ArgumentNullException(nameof(messageService));

            Items = new ReadOnlyObservableCollection<RecentDocumentItemViewModel>(
                items);
            openCommand = new AsyncRelayCommand(
                OpenAsync,
                parameter => parameter is RecentDocumentItemViewModel
                {
                    IsOpening: false
                });
            removeCommand = new AsyncRelayCommand(
                RemoveAsync,
                parameter => parameter is RecentDocumentItemViewModel);
            relocateCommand = new AsyncRelayCommand(
                RelocateAsync,
                parameter => parameter is RecentDocumentItemViewModel
                {
                    IsMissing: true
                });
            refreshAvailabilityCommand = new RelayCommand(
                RefreshAvailability,
                parameter => parameter is RecentDocumentItemViewModel);

            LocalizationManager.CultureChanged += OnCultureChanged;
        }

        public ReadOnlyObservableCollection<RecentDocumentItemViewModel> Items { get; }
        public bool HasItems => Items.Count > 0;

        public ICommand OpenCommand => openCommand;
        public ICommand RemoveCommand => removeCommand;
        public ICommand RelocateCommand => relocateCommand;
        public ICommand RefreshAvailabilityCommand => refreshAvailabilityCommand;

        public async Task InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            if(initialized)
                return;

            try
            {
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
                await ShowErrorAsync("RecentDocuments.LoadFailed", exception);
            }
        }

        private async Task OpenAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            if(parameter is not RecentDocumentItemViewModel item)
                return;

            item.RefreshAvailability();
            if(item.IsMissing)
            {
                RaiseCommandStates();
                await messageService.ShowMessage(
                    LocalizationManager.GetString("RecentDocuments.ErrorTitle"),
                    LocalizationManager.Format(
                        "RecentDocuments.MissingFile",
                        item.FileName));
                return;
            }

            item.IsOpening = true;
            openCommand.RaiseCanExecuteChanged();
            try
            {
                WorkspaceFileOpenResult result = await fileOpenService.OpenAsync(
                    item.Path,
                    cancellationToken);
                if(result.Cancelled)
                    return;

                if(!result.Success)
                {
                    Debug.WriteLine(result.Error);
                    await messageService.ShowMessage(
                        LocalizationManager.GetString("RecentDocuments.ErrorTitle"),
                        LocalizationManager.Format(
                            "RecentDocuments.OpenFailed",
                            item.FileName));
                }
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception exception)
            {
                await ShowErrorAsync("RecentDocuments.OpenFailedGeneric", exception);
            }
            finally
            {
                item.IsOpening = false;
                openCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task RemoveAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            if(parameter is not RecentDocumentItemViewModel item)
                return;

            try
            {
                await service.RemoveAsync(item.Path, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception exception)
            {
                await ShowErrorAsync("RecentDocuments.SaveFailed", exception);
            }
        }

        private async Task RelocateAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            if(parameter is not RecentDocumentItemViewModel item)
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
                    LocalizationManager.GetString("RecentDocuments.ErrorTitle"),
                    LocalizationManager.Format(
                        "RecentDocuments.RelocateInvalid",
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
                await ShowErrorAsync("RecentDocuments.RelocateFailed", exception);
            }
        }

        private void RefreshAvailability(object? parameter)
        {
            if(parameter is not RecentDocumentItemViewModel item)
                return;

            item.RefreshAvailability();
            RaiseCommandStates();
        }

        private void Rebuild()
        {
            items.Clear();
            foreach(RecentDocument document in service.Items
                .OrderByDescending(item => item.LastAccessedAtUtc))
            {
                items.Add(new RecentDocumentItemViewModel(document));
            }

            OnPropertyChanged(nameof(HasItems));
            RaiseCommandStates();
        }

        private void RaiseCommandStates()
        {
            openCommand.RaiseCanExecuteChanged();
            removeCommand.RaiseCanExecuteChanged();
            relocateCommand.RaiseCanExecuteChanged();
        }

        private async Task ShowErrorAsync(string resourceKey, Exception exception)
        {
            Debug.WriteLine(exception);
            await messageService.ShowMessage(
                LocalizationManager.GetString("RecentDocuments.ErrorTitle"),
                LocalizationManager.GetString(resourceKey));
        }

        private static string GetNativePath(string path)
        {
            const string localPrefix = "local://";
            string trimmed = path.Trim();
            return trimmed.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase)
                ? trimmed[localPrefix.Length..]
                : trimmed;
        }

        private void OnServiceChanged(object? sender, EventArgs args) => Rebuild();

        private void OnCultureChanged(object? sender, EventArgs args) => Rebuild();

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            service.Changed -= OnServiceChanged;
            LocalizationManager.CultureChanged -= OnCultureChanged;
        }
    }
}
