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
        private readonly ISecureFileProcessor secureFileProcessor;

        public MenuFileModel(
            IWindowManager windowManager,
            IFlowDocumentSaveService saveService,
            IRichTextBoxService richTextBox,
            IDocumentSession documentSession,
            IDocumentSaveTargetPicker saveTargetPicker,
            IProgressDialogService progressDialogService,
            IMessageService messageService,
            IDocumentRecoveryService recoveryService,
            ISecureFileProcessor secureFileProcessor)
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
            this.secureFileProcessor = secureFileProcessor
                ?? throw new ArgumentNullException(
                    nameof(secureFileProcessor));

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
            var id = windowManager.CreateWindow<FileExplorer>();
            windowManager.ShowWindow(id);
        }

        public bool CanExecute_SaveFile(object? obj)
        {
            return !richTextBox.IsReadOnly &&
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
            return !richTextBox.IsReadOnly;
        }
        public Task Execute_SaveAsFileAsync(
            object? obj,
            CancellationToken cancellationToken)
        {
            return SaveAndIgnoreResultAsync(
                forceChooseTarget: true,
                cancellationToken);
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
            try
            {
                DocumentSaveTarget? target =
                    GetSaveTarget(forceChooseTarget);
                if(target is null)
                    return false;

                long savedRevision = documentSession.Revision;

                await progressDialogService.RunAsync(
                    "Сохранение документа",
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
                    "Ошибка сохранения",
                    exception.Message);
                return false;
            }
        }

        private async Task SaveEncryptedAsync(
            string filePath,
            CancellationToken cancellationToken,
            IProgressReporter progress)
        {
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
            return true;
        }
        public void Execute_CloseFile(object? obj)
        {
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
