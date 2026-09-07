using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class MenuContentViewModel:
        ViewModelBase,
        IMenuContentViewModel,
        ICommandRegistry
    {
        private readonly IWindowManager windowManager;
        private readonly ICommandService commandService;
        private readonly IImageFilePicker imageFilePicker;
        private readonly IImageContentLoader imageLoader;
        private readonly IDocumentImageInserter imageInserter;
        private readonly IMessageService messages;
        private readonly IMarkdownDocumentState? markdownDocument;

        public MenuContentViewModel(
            IWindowManager windowManager,
            ICommandService commandService,
            IImageFilePicker imageFilePicker,
            IImageContentLoader imageLoader,
            IDocumentImageInserter imageInserter,
            IMessageService messages,
            IMarkdownDocumentState? markdownDocument = null)
        {
            this.windowManager = windowManager
                ?? throw new ArgumentNullException(nameof(windowManager));
            this.commandService = commandService
                ?? throw new ArgumentNullException(nameof(commandService));
            this.imageFilePicker = imageFilePicker
                ?? throw new ArgumentNullException(nameof(imageFilePicker));
            this.imageLoader = imageLoader
                ?? throw new ArgumentNullException(nameof(imageLoader));
            this.imageInserter = imageInserter
                ?? throw new ArgumentNullException(nameof(imageInserter));
            this.messages = messages
                ?? throw new ArgumentNullException(nameof(messages));
            this.markdownDocument = markdownDocument;
            if(markdownDocument is not null)
            {
                markdownDocument.PropertyChanged += (_, args) =>
                {
                    if(args.PropertyName == nameof(
                        IMarkdownDocumentState.IsActive))
                    {
                        insertImage?.RaiseCanExecuteChanged();
                    }
                };
            }

            RegistryCommands();
        }

        public ICommand Reading => NoOpCommand;
        public ICommand InsertText => NoOpCommand;
        public ICommand OpenDocumentTree => NoOpCommand;

        public ICommand InsertImage => insertImage ??=
            new AsyncRelayCommand(
                (_, cancellationToken) =>
                    InsertImageAsync(cancellationToken),
                _ => markdownDocument?.IsActive != true);
        private AsyncRelayCommand? insertImage;

        public ICommand MediaPlayer => mediaPlayer ??=
            new RelayCommand(_ => OpenMediaPlayer());
        private RelayCommand? mediaPlayer;

        public ICommand Loaded => NoOpCommand;
        public ICommand Close => NoOpCommand;
        public ICommand Closing => NoOpCommand;
        public ICommand Closed => NoOpCommand;

        private static ICommand NoOpCommand { get; } =
            new RelayCommand(_ => { });

        public void RegistryCommands()
        {
            commandService.Register(
                CommandKey.menuContent_InsertImage,
                InsertImage);
            commandService.Register(
                CommandKey.menuContent_MediaPlayer,
                MediaPlayer);
        }

        private async Task InsertImageAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                string? path = await imageFilePicker.PickImageAsync(
                    cancellationToken);
                if(string.IsNullOrWhiteSpace(path))
                    return;

                var image = await imageLoader.LoadFromFileAsync(
                    path,
                    cancellationToken);
                await imageInserter.InsertAsync(
                    image,
                    cancellationToken);
            }
            catch(OperationCanceledException)
            {
                // Отмена пользователем не является ошибкой.
            }
            catch(Exception exception)
            {
                await messages.ShowMessage(
                    LocalizationManager.GetString(
                        "Document.InsertImageError"),
                    exception.Message);
            }
        }

        private void OpenMediaPlayer()
        {
            Guid id = windowManager.CreateWindow<MediaPlayer>();
            windowManager.ShowWindow(id);
        }
    }
}
