using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.ComponentModel;

namespace CryptoBook.Services
{
    public sealed class DocumentTitleProvider:
        ViewModelBase,
        IDocumentTitleProvider
    {
        private readonly IDocumentSession documentSession;
        private readonly IFileDisplayNameService fileDisplayNameService;
        private bool disposed;

        public DocumentTitleProvider(
            IDocumentSession documentSession,
            IFileDisplayNameService fileDisplayNameService)
        {
            this.documentSession = documentSession ??
                throw new ArgumentNullException(nameof(documentSession));
            this.fileDisplayNameService = fileDisplayNameService ??
                throw new ArgumentNullException(nameof(fileDisplayNameService));
            this.documentSession.PropertyChanged += OnDocumentSessionPropertyChanged;
        }

        public string Title =>
            fileDisplayNameService.GetDisplayName(
                documentSession.FilePath ?? documentSession.DisplayName);

        public string? Path => documentSession.FilePath;

        private void OnDocumentSessionPropertyChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName is nameof(IDocumentSession.FilePath) or
               nameof(IDocumentSession.DisplayName))
                OnPropertyChanged(nameof(Title), nameof(Path));
        }

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            documentSession.PropertyChanged -= OnDocumentSessionPropertyChanged;
        }
    }
}
