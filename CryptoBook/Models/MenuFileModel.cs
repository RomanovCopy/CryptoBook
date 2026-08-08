using Autofac;

using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Views;

using System.IO;
using System.Security.Cryptography;
using System.Windows.Input;

namespace CryptoBook.Models
{
    /// <summary>
    /// Реализует команды файлового меню и единый конвейер обычного
    /// и защищённого сохранения текущего документа.
    /// </summary>
    public class MenuFileModel: ViewModelBase, IMenuFileModel
    {
        private readonly IWindowManager windowManager;
        private readonly IFlowDocumentSaveService saveService;
        private readonly IRichTextBoxService richTextBox;
        private readonly IDocumentSession documentSession;
        private readonly IDocumentSaveTargetPicker saveTargetPicker;
        private readonly IProgressDialogService progressDialogService;
        private readonly IMessageService messageService;
        private readonly IDocumentRecoveryService recoveryService;
        private readonly IDocumentDialogService documentDialogService;
        private readonly ISecureFileProcessor secureFileProcessor;
        private readonly IDocumentContentInspector documentContentInspector;
        private readonly IDocumentPrintService documentPrintService;
        private readonly IDocumentSaveEncryptionPolicy saveEncryptionPolicy;
        private readonly IKeyResetService? keyResetService;

        public MenuFileModel(
            IWindowManager windowManager,
            IFlowDocumentSaveService saveService,
            IRichTextBoxService richTextBox,
            IDocumentSession documentSession,
            IDocumentSaveTargetPicker saveTargetPicker,
            IProgressDialogService progressDialogService,
            IMessageService messageService,
            IDocumentRecoveryService recoveryService,
            IDocumentDialogService documentDialogService,
            ISecureFileProcessor secureFileProcessor,
            IDocumentContentInspector documentContentInspector,
            IDocumentPrintService documentPrintService,
            IDocumentSaveEncryptionPolicy saveEncryptionPolicy,
            IKeyResetService? keyResetService = null)
        {
            this.windowManager = windowManager
                ?? throw new ArgumentNullException(nameof(windowManager));
            this.saveService = saveService
                ?? throw new ArgumentNullException(nameof(saveService));
            this.richTextBox = richTextBox
                ?? throw new ArgumentNullException(nameof(richTextBox));
            this.documentSession = documentSession
                ?? throw new ArgumentNullException(nameof(documentSession));
            this.saveTargetPicker = saveTargetPicker
                ?? throw new ArgumentNullException(nameof(saveTargetPicker));
            this.progressDialogService = progressDialogService
                ?? throw new ArgumentNullException(
                    nameof(progressDialogService));
            this.messageService = messageService
                ?? throw new ArgumentNullException(nameof(messageService));
            this.recoveryService = recoveryService
                ?? throw new ArgumentNullException(nameof(recoveryService));
            this.documentDialogService = documentDialogService
                ?? throw new ArgumentNullException(
                    nameof(documentDialogService));
            this.secureFileProcessor = secureFileProcessor
                ?? throw new ArgumentNullException(
                    nameof(secureFileProcessor));
            this.documentContentInspector = documentContentInspector
                ?? throw new ArgumentNullException(
                    nameof(documentContentInspector));
            this.documentPrintService = documentPrintService
                ?? throw new ArgumentNullException(
                    nameof(documentPrintService));
            this.saveEncryptionPolicy = saveEncryptionPolicy
                ?? throw new ArgumentNullException(
                    nameof(saveEncryptionPolicy));
            this.keyResetService = keyResetService;

            documentSession.PropertyChanged += (_, _) =>
                OnPropertyChanged(nameof(documentSession));
        }

        public bool CanExecute_NewFile(object? obj)
        {
            return true;
        }
        public void Execute_NewFile(object? obj)
        {
            var id = windowManager.CreateWindow<NewFileDialog>();
            windowManager.ShowWindow(id);
        }

        public bool CanExecute_OpenFile(object? obj)
        {
            return true;
        }
        public void Execute_OpenFile(object? obj)
        {
            if(keyResetService?.State is KeyResetState.Resetting or KeyResetState.Restoring)
                return;
            keyResetService?.NotifyActivity();
            var id = windowManager.CreateWindow<FileExplorer>();
            windowManager.ShowWindow(id);
        }

        public bool CanExecute_SaveFile(object? obj)
        {
            return keyResetService?.State is not (KeyResetState.Resetting or KeyResetState.Restoring) &&
                !richTextBox.IsReadOnly &&
                (documentSession.IsDirty ||
                 string.IsNullOrWhiteSpace(documentSession.FilePath));
        }
        public Task Execute_SaveFileAsync(
            object? obj,
            CancellationToken cancellationToken)
        {
            return SaveAndIgnoreResultAsync(
                forceChooseTarget: false,
                cancellationToken);
        }

        public bool CanExecute_SaveAsFile(object? obj)
        {
            return keyResetService?.State is not (KeyResetState.Resetting or KeyResetState.Restoring) &&
                !richTextBox.IsReadOnly;
        }
        public Task Execute_SaveAsFileAsync(
            object? obj,
            CancellationToken cancellationToken)
        {
            return SaveAndIgnoreResultAsync(
                forceChooseTarget: true,
                cancellationToken);
        }

        public bool CanExecute_PrintFile(object? obj) =>
            documentSession.HasDocument &&
            documentContentInspector.HasPrintableContent(
                richTextBox.Document);

        public async Task Execute_PrintFileAsync(
            object? obj,
            CancellationToken cancellationToken)
        {
            if(!CanExecute_PrintFile(obj))
                return;

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string documentName = string.IsNullOrWhiteSpace(
                    documentSession.FilePath)
                    ? documentSession.DisplayName
                    : Path.GetFileName(documentSession.FilePath);
                documentPrintService.Print(
                    richTextBox.Document,
                    documentName);
            }
            catch(Exception exception)
            {
                await messageService.ShowMessage(
                    LocalizationManager.GetString("Document.PrintError"),
                    exception.Message);
            }
        }

        public Task<bool> TrySaveCurrentAsync(
            CancellationToken cancellationToken = default) =>
            SaveAsync(forceChooseTarget: false, cancellationToken);

        private async Task SaveAndIgnoreResultAsync(
            bool forceChooseTarget,
            CancellationToken cancellationToken)
        {
            await SaveAsync(forceChooseTarget, cancellationToken);
        }

        private async Task<bool> SaveAsync(
            bool forceChooseTarget,
            CancellationToken cancellationToken)
        {
            using IDisposable? timerPause = keyResetService?.Pause();
            try
            {
                DocumentSaveTarget? target =
                    GetSaveTarget(forceChooseTarget);
                if(target is null)
                    return false;

                bool sourceIsPlaintextFile =
                    !string.IsNullOrWhiteSpace(documentSession.FilePath) &&
                    documentSession.Template is not SecureFileTemplate;
                target = await saveEncryptionPolicy.ResolveAsync(
                    target,
                    sourceIsPlaintextFile);
                if(target is null)
                    return false;

                // Фиксируем ревизию до асинхронной записи: последующие правки
                // должны оставить документ в состоянии IsDirty.
                long savedRevision = documentSession.Revision;

                await progressDialogService.RunAsync(
                    LocalizationManager.GetString(
                        "Document.SaveOperation"),
                    async (progress, dialogToken) =>
                    {
                        using var linkedTokenSource =
                            CancellationTokenSource
                                .CreateLinkedTokenSource(
                                    cancellationToken,
                                    dialogToken);
                        if(target.Template is SecureFileTemplate)
                        {
                            await SaveEncryptedAsync(
                                target.FilePath,
                                linkedTokenSource.Token,
                                progress);
                        }
                        else
                        {
                            await saveService.SaveToFileAsync(
                                richTextBox,
                                target.FilePath,
                                target.Template,
                                linkedTokenSource.Token,
                                progress);
                        }
                        return true;
                    });

                documentSession.MarkSaved(
                    target.FilePath,
                    target.Template,
                    savedRevision);
                if(!documentSession.IsDirty)
                    await recoveryService.DeleteSnapshotAsync();
                return true;
            }
            catch(OperationCanceledException)
            {
                return false;
            }
            catch(Exception exception)
            {
                await messageService.ShowMessage(
                    LocalizationManager.GetString("Document.SaveError"),
                    exception.Message);
                return false;
            }
        }

        private async Task SaveEncryptedAsync(
            string filePath,
            CancellationToken cancellationToken,
            IProgressReporter progress)
        {
            // XamlPackage сначала формируется в памяти, чтобы открытая версия
            // документа не появлялась во временном файле на диске.
            await using MemoryStream plaintext = new();
            try
            {
                await saveService.SaveToStreamAsync(
                    richTextBox,
                    plaintext,
                    new XamlPackageFileTemplate(),
                    cancellationToken,
                    progress);
                plaintext.Position = 0;
                await secureFileProcessor.EncryptStreamAsync(
                    plaintext,
                    ".XamlPackage",
                    filePath,
                    progress,
                    cancellationToken);
            }
            finally
            {
                // MemoryStream.Dispose не очищает массив; открытый текст
                // затирается явно после завершения или ошибки шифрования.
                if(plaintext.TryGetBuffer(
                    out ArraySegment<byte> buffer))
                {
                    CryptographicOperations.ZeroMemory(
                        buffer.AsSpan(0, (int)plaintext.Length));
                }
            }
        }

        private DocumentSaveTarget? GetSaveTarget(
            bool forceChooseTarget)
        {
            if(!forceChooseTarget &&
               !string.IsNullOrWhiteSpace(documentSession.FilePath) &&
               documentSession.Template is not null)
            {
                return new DocumentSaveTarget(
                    documentSession.FilePath,
                    documentSession.Template);
            }

            return saveTargetPicker.Pick(
                documentSession.FilePath ?? documentSession.DisplayName,
                documentSession.Template);
        }


        public bool CanExecute_FileOverview(object? obj)
        {
            return true;
        }
        public void Execute_FileOverview(object? obj)
        {
        }

        public bool CanExecute_OpenDirectory(object? obj)
        {
            return true;
        }
        public void Execute_OpenDirectory(object? obj)
        {
        }

        public bool CanExecute_WorkingDirectorySynchronization(object? obj)
        {
            return true;
        }
        public void Execute_WorkingDirectorySynchronization(object? obj)
        {
        }

        public bool CanExecute_CloseFile(object? obj)
        {
            return documentSession.HasDocument &&
                keyResetService?.State is not (KeyResetState.Resetting or KeyResetState.Restoring);
        }
        public async Task Execute_CloseFileAsync(
            object? obj,
            CancellationToken cancellationToken)
        {
            using IDisposable? timerPause = keyResetService?.Pause();
            if(!documentSession.HasDocument)
                return;

            if(documentSession.IsDirty)
            {
                UnsavedChangesChoice choice =
                    documentDialogService.ConfirmCloseWithUnsavedChanges();
                if(choice == UnsavedChangesChoice.Cancel)
                    return;

                if(choice == UnsavedChangesChoice.Save)
                {
                    bool saved = await TrySaveCurrentAsync(cancellationToken);
                    if(!saved || documentSession.IsDirty)
                        return;
                }
            }

            try
            {
                await recoveryService.DeleteSnapshotAsync();
            }
            catch(Exception exception)
            {
                documentDialogService.ShowRecoveryCleanupError(exception);
            }

            documentSession.Close();
        }

        public bool CanExecute_UpdateFile(object? obj)
        {
            return true;
        }
        public void Execute_UpdateFile(object? obj)
        {
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
        }

        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        public void Execute_Closed(object? obj)
        {
        }

    }
}
