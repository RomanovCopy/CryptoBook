using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class NewFileDialogModel: ViewModelBase, INewFileDialogModel, IWindowWithId
    {
        private readonly IFileTemplateRegistry _registry;
        private readonly IFileCreationService _creator;
        private readonly IFileManagerService _fileManager;
        private readonly IFolderPickerService _folderPicker;
        private readonly ICommandService _commandService;
        private readonly IWindowManager _windowManager;

        private CancellationTokenSource? _cts;

        public event Func<FileOperationResult, Task>? CloseRequested; // VM → View (успех/ошибка)

        public Guid WindowId { get => windowId; private set => SetProperty(ref windowId, value); }
        Guid windowId;

        public IReadOnlyList<IFileTemplate> Templates { get => templates; private set => SetProperty(ref templates, value); }
        IReadOnlyList<IFileTemplate> templates;

        public IFileTemplate? SelectedTemplate { get => template; set => SetProperty(ref template, value); }
        IFileTemplate? template;

        public string FileName { get => fileName ?? string.Empty; set => SetProperty(ref fileName, value ?? string.Empty); }
        string? fileName;

        public IfExistsMode IfExists { get => ifExist = IfExistsMode.AutoRename; set => SetProperty(ref ifExist, value); }
        IfExistsMode ifExist;

        public bool CreateDirectoryIfMissing { get => createDirectoryIfMissing; set => SetProperty(ref createDirectoryIfMissing, value); }
        bool createDirectoryIfMissing;

        public string TargetDirectory { get => targetDirectory ?? string.Empty; set => SetProperty(ref targetDirectory, value); }
        string? targetDirectory;

        public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
        string? errorMessage;

        public bool IsReadOnly { get => isReadOnly; set => SetProperty(ref isReadOnly, value); }
        bool isReadOnly;

        public bool IsHidden { get => isHidden; set => SetProperty(ref isHidden, value); }
        bool isHidden;

        public bool CanWrite { get => canWrite; set => SetProperty(ref canWrite, value); }
        bool canWrite;

        public bool ShowHiddenFiles { get => showHiddenFiles; set => SetProperty(ref showHiddenFiles, value); }
        bool showHiddenFiles;

        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                SetProperty(ref isBusy, value);
                (_commandService.GetCommand(CommandKey.NewFileDialog_Create) as IRaiseCanExecuteChanged)?.RaiseCanExecuteChanged();
            }
        }
        bool isBusy;

        public NewFileDialogModel(IFileTemplateRegistry registry, IFileCreationService creator, IFileManagerService fileManager, IFolderPickerService folderPicker, ICommandService commandService, IWindowManager windowManager)
        {
            WindowId = Guid.NewGuid();
            _registry = registry;
            _creator = creator;
            _fileManager = fileManager;
            _folderPicker = folderPicker;
            _fileManager = fileManager;
            _commandService = commandService;
            _windowManager = windowManager;
            Templates = _registry.GetAll();
            SelectedTemplate = Templates.FirstOrDefault();
        }





        public bool CanExceute_InitSuggested(object? obj)
        {
            return SelectedTemplate is not null;
        }
        public void Execute_InitSuggested(object? obj)
        {
            if(obj is string targetDirectory)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _ = InitSuggestedAsync(targetDirectory, ct);
            }
        }

        public bool CanExecute_Browse(object? obj)
        {
            return !IsBusy;
        }
        public void Execute_Browse(object? obj)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _ = BrowseAsync(ct);
        }


        public bool CanExecute_SelectedNewTemplate(object? obj)
        {
            return FileName.Length > 3 && SelectedTemplate is not null;
        }

        public void Execute_SelectedNewTemplate(object? obj)
        {
            FileName = FileName.Split('.')[0].Trim() + SelectedTemplate?.DefaultExtension;
        }

        public bool CanExecute_CreateDirectory(object? obj)
        {
            return true;
        }
        public void Execute_CreateDirectory(object? obj)
        {
        }

        public bool CanExecute_Create(object? obj)
        {
            return CanCreate();
        }
        public void Execute_Create(object? obj)
        {
            if(obj is string targetDirectory)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _ = CreateAsync(targetDirectory, ct);
                _windowManager.CloseWindow(WindowId);
            }
        }

        public bool CanExecute_Cancel(object? obj)
        {
            return _cts is not null;
        }
        public void Execute_Cancel(object? obj)
        {
            _cts?.Cancel();
            _windowManager.CloseWindow(WindowId);
        }





        public bool CanExecute_Close(object? obj)
        {
            return true;
        }
        public void Execute_Close(object? obj)
        {
        }


        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
        }



        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
            _cts?.Dispose();
            _cts = null;
        }


        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        public void Execute_Closed(object? obj)
        {
        }





        private string Normalize(string path) => _fileManager.NormalizePath(path);

        private async Task InitSuggestedAsync(string targetDirectory, CancellationToken ct)
        {
            if(SelectedTemplate is null)
                return;
            FileName = await _creator.SuggestUniqueNameAsync(targetDirectory, SelectedTemplate, ct);
        }

        private async Task<FileOperationResult> CreateAsync(string targetDirectory, CancellationToken ct)
        {
            if(SelectedTemplate is null)
                return FileOperationResult.Fail("Не выбран тип файла.");

            return await _creator.CreateAsync(targetDirectory, FileName, SelectedTemplate, IfExists, IsHidden, IsReadOnly, ct);
        }


        // Инициализация из панели (перед показом диалога)
        public async Task InitializeAsync(string initialDirectory, CancellationToken ct)
        {
            TargetDirectory = initialDirectory;
            await CheckDirectoryAsync(ct);

            if(SelectedTemplate != null)
                FileName = await _creator.SuggestUniqueNameAsync(Normalize(TargetDirectory), SelectedTemplate, ct);
        }


        private bool CanCreate() =>
        !IsBusy &&
        SelectedTemplate != null &&
        !string.IsNullOrWhiteSpace(TargetDirectory) &&
        !string.IsNullOrWhiteSpace(FileName) &&
        (CanWrite || CreateDirectoryIfMissing);


        private async Task BrowseAsync(CancellationToken ct)
        {
            var chosen = await _folderPicker.PickFolderAsync(TargetDirectory, ct);
            if(chosen != null)
            {
                TargetDirectory = chosen;
                await CheckDirectoryAsync(ct);

                if(SelectedTemplate != null && string.IsNullOrWhiteSpace(FileName))
                    FileName = await _creator.SuggestUniqueNameAsync(Normalize(TargetDirectory), SelectedTemplate, ct);
            }
        }


        private async Task CheckDirectoryAsync(CancellationToken ct)
        {
            try
            {
                IsBusy = true;
                ErrorMessage = null;
                CanWrite = false;

                var normalized = Normalize(TargetDirectory);
                // если директории ещё нет — CanWrite=false, но можно создать при OK
                bool exists = await DirectoryExistsAsync(normalized, ct);
                CanWrite = exists && await _fileManager.CanWriteAsync(normalized, ct);
            } catch(Exception ex)
            {
                ErrorMessage = ex.Message;
            } finally
            {
                IsBusy = false;
            }
        }


        private async Task<bool> DirectoryExistsAsync(string normalizedPath, CancellationToken ct)
        {
            // мягкая проверка: попытаться прочитать содержимое
            try
            {
                _ = await _fileManager.BrowseAsync(normalizedPath, ct, ShowHiddenFiles);
                return true;
            } catch(DirectoryNotFoundException) { return false; } catch(IOException io) when(io.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)) { return false; } catch { return false; }
        }


        private async Task<FileOperationResult> EnsureDirectoryAsync(string normalizedPath, CancellationToken ct)
        {
            // создадим «как есть» (для local это просто CreateDirectory); для zip/ssh будет своя логика в провайдере
            try
            {
                return await _fileManager.CreateDirectoryAsync(normalizedPath, string.Empty, ct);
            } catch(Exception ex)
            {
                return FileOperationResult.Fail(ex.Message);
            }
        }

    }
}
