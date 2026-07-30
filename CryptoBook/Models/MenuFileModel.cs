using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

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

        public MenuFileModel(
            IWindowManager windowManager,
            IFlowDocumentSaveService saveService,
            IRichTextBoxService richTextBox,
            IDocumentSession documentSession,
            IDocumentSaveTargetPicker saveTargetPicker,
            IProgressDialogService progressDialogService,
            IMessageService messageService)
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
            return SaveAsync(
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
            return SaveAsync(
                forceChooseTarget: true,
                cancellationToken);
        }

        private async Task SaveAsync(
            bool forceChooseTarget,
            CancellationToken cancellationToken)
        {
            try
            {
                DocumentSaveTarget? target =
                    GetSaveTarget(forceChooseTarget);
                if(target is null)
                    return;

                await progressDialogService.RunAsync(
                    "Сохранение документа",
                    async (progress, dialogToken) =>
                    {
                        using var linkedTokenSource =
                            CancellationTokenSource
                                .CreateLinkedTokenSource(
                                    cancellationToken,
                                    dialogToken);
                        await saveService.SaveToFileAsync(
                            richTextBox,
                            target.FilePath,
                            target.Template,
                            linkedTokenSource.Token,
                            progress);
                        return true;
                    });

                documentSession.MarkSaved(
                    target.FilePath,
                    target.Template);
            }
            catch(OperationCanceledException)
            {
            }
            catch(Exception exception)
            {
                await messageService.ShowMessage(
                    "Ошибка сохранения",
                    exception.Message);
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
                documentSession.FilePath,
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
