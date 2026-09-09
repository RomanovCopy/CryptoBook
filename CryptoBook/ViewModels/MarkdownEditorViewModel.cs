using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.ComponentModel;
using System.Windows.Documents;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class MarkdownEditorViewModel:
        ViewModelBase,
        IMarkdownEditorViewModel
    {
        private readonly IMarkdownDocumentState markdownDocument;
        private readonly IMarkdownFlowDocumentRenderer renderer;
        private readonly IUriNavigationService uriNavigationService;
        private readonly IMenuFileViewModel menuFile;
        private readonly IDocumentSession documentSession;
        private readonly IPageNavigationService navigationService;
        private bool isPreviewMode;
        private FlowDocument? previewDocument;

        public MarkdownEditorViewModel(
            IMarkdownDocumentState markdownDocument,
            IMarkdownFlowDocumentRenderer renderer,
            IUriNavigationService uriNavigationService,
            IMenuFileViewModel menuFile,
            IDocumentSession documentSession,
            IPageNavigationService navigationService)
        {
            this.markdownDocument = markdownDocument ??
                throw new ArgumentNullException(nameof(markdownDocument));
            this.renderer = renderer ??
                throw new ArgumentNullException(nameof(renderer));
            this.uriNavigationService = uriNavigationService ??
                throw new ArgumentNullException(nameof(uriNavigationService));
            this.menuFile = menuFile ??
                throw new ArgumentNullException(nameof(menuFile));
            this.documentSession = documentSession ??
                throw new ArgumentNullException(nameof(documentSession));
            this.navigationService = navigationService ??
                throw new ArgumentNullException(nameof(navigationService));

            markdownDocument.PropertyChanged += OnMarkdownDocumentChanged;
            LocalizationManager.CultureChanged += OnCultureChanged;
        }

        public string MarkdownText
        {
            get => markdownDocument.Text;
            set => markdownDocument.Text = value;
        }

        public bool IsPreviewMode
        {
            get => isPreviewMode;
            private set
            {
                if(SetProperty(ref isPreviewMode, value))
                    OnPropertyChanged(nameof(ModeLabel), nameof(ToggleViewText));
            }
        }

        public string ModeLabel => LocalizationManager.GetString(
            IsPreviewMode ? "Editor.Preview" : "Editor.Editing");

        public string ToggleViewText => LocalizationManager.GetString(
            IsPreviewMode ? "Editor.Editor" : "Editor.Preview");

        public FlowDocument? PreviewDocument
        {
            get => previewDocument;
            private set => SetProperty(ref previewDocument, value);
        }

        public ICommand ToggleView => toggleView ??=
            new RelayCommand(_ => SetPreviewMode(!IsPreviewMode));
        private RelayCommand? toggleView;

        public ICommand OpenSyntaxHelp => openSyntaxHelp ??=
            new RelayCommand(_ => navigationService.Navigate(
                "MarkdownSyntaxHelp"));
        private RelayCommand? openSyntaxHelp;

        public ICommand OpenHyperlink => openHyperlink ??=
            new RelayCommand(
                parameter =>
                {
                    if(parameter is Uri uri)
                        uriNavigationService.TryOpen(uri);
                },
                parameter => parameter is Uri uri &&
                    uri.IsAbsoluteUri &&
                    (uri.Scheme == Uri.UriSchemeHttp ||
                     uri.Scheme == Uri.UriSchemeHttps ||
                     uri.Scheme == Uri.UriSchemeMailto));
        private RelayCommand? openHyperlink;

        public ICommand SaveDocument => menuFile.SaveFile;
        public ICommand SaveDocumentAs => menuFile.SaveAsFile;

        public ICommand PageLoaded => noOperation ??=
            new RelayCommand(_ => { });
        public ICommand PageClear => noOperation ??=
            new RelayCommand(_ => { });
        public ICommand Loaded => noOperation ??=
            new RelayCommand(_ => { });
        public ICommand Close => noOperation ??=
            new RelayCommand(_ => { });
        public ICommand Closing => noOperation ??=
            new RelayCommand(_ => { });
        public ICommand Closed => closed ??=
            new RelayCommand(_ =>
            {
                markdownDocument.PropertyChanged -= OnMarkdownDocumentChanged;
                LocalizationManager.CultureChanged -= OnCultureChanged;
            });
        private RelayCommand? noOperation;
        private RelayCommand? closed;

        private void SetPreviewMode(bool value)
        {
            if(value == IsPreviewMode)
                return;

            if(!value)
            {
                PreviewDocument = null;
                IsPreviewMode = false;
                return;
            }

            // The preview is a fresh one-way projection of the current source.
            PreviewDocument = renderer.Render(
                markdownDocument.Text,
                documentSession.FilePath);
            IsPreviewMode = true;
        }

        private void OnMarkdownDocumentChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName == nameof(IMarkdownDocumentState.Text))
                OnPropertyChanged(nameof(MarkdownText));
            if(args.PropertyName == nameof(
                IMarkdownDocumentState.DocumentVersion))
            {
                PreviewDocument = null;
                IsPreviewMode = false;
            }
        }

        private void OnCultureChanged(object? sender, EventArgs args) =>
            OnPropertyChanged(nameof(ModeLabel), nameof(ToggleViewText));
    }
}
