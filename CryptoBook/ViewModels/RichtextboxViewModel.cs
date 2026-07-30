using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Windows.Input;
using System.Windows.Documents;

namespace CryptoBook.ViewModels
{
    public class RichtextboxViewModel: ViewModelBase, IRichtextboxViewModel
    {
        private readonly IRichtextboxModel richtextboxModel;
        private readonly IRichTextBoxService richTextBox;
        private readonly IDocumentPreviewService previewService;
        private readonly IUriNavigationService uriNavigationService;
        private readonly IMenuFileViewModel menuFile;
        private bool isPreviewMode;
        private FlowDocument? previewDocument;

        public RichtextboxViewModel(
            IRichtextboxModel richtextboxModel,
            IRichTextBoxService richTextBox,
            IDocumentPreviewService previewService,
            IUriNavigationService uriNavigationService,
            IMenuFileViewModel menuFile)
        {
            this.richtextboxModel = richtextboxModel ??
                throw new ArgumentNullException(nameof(richtextboxModel));
            this.richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
            this.previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
            this.uriNavigationService = uriNavigationService ??
                throw new ArgumentNullException(nameof(uriNavigationService));
            this.menuFile = menuFile
                ?? throw new ArgumentNullException(nameof(menuFile));
            richtextboxModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
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

        public string ModeLabel =>
            IsPreviewMode ? "Постраничный просмотр" : "Редактирование";

        public string ToggleViewText =>
            IsPreviewMode ? "Редактор" : "Просмотр";

        public FlowDocument? PreviewDocument
        {
            get => previewDocument;
            private set => SetProperty(ref previewDocument, value);
        }

        public ICommand ToggleView => toggleView ??=
            new RelayCommand(_ => SetPreviewMode(!IsPreviewMode));
        private RelayCommand? toggleView;

        public ICommand OpenHyperlink => openHyperlink ??=
            new RelayCommand(
                parameter =>
                {
                    if(parameter is Uri uri)
                        uriNavigationService.TryOpen(uri);
                },
                parameter => parameter is Uri);
        private RelayCommand? openHyperlink;

        public ICommand SaveDocument => menuFile.SaveFile;
        public ICommand SaveDocumentAs => menuFile.SaveAsFile;

        public ICommand Loaded => loaded ??=
            new RelayCommand(richtextboxModel.Execute_Loaded, richtextboxModel.CanExecute_Loaded);
        private RelayCommand? loaded;

        public ICommand Close => close ??=
            new RelayCommand(richtextboxModel.Execute_Close, richtextboxModel.CanExecute_Close);
        private RelayCommand? close;

        public ICommand Closing => closing ??=
            new RelayCommand(richtextboxModel.Execute_Closing, richtextboxModel.CanExecute_Closing);
        private RelayCommand? closing;

        public ICommand Closed => closed ??=
            new RelayCommand(richtextboxModel.Execute_Closed, richtextboxModel.CanExecute_Closed);
        private RelayCommand? closed;

        private void SetPreviewMode(bool previewMode)
        {
            if(previewMode == IsPreviewMode)
                return;

            if(!previewMode)
            {
                PreviewDocument = null;
                IsPreviewMode = false;
                richTextBox.Focus();
                return;
            }

            PreviewDocument = previewService.CreatePreview(richTextBox.Document);
            IsPreviewMode = true;
        }
    }
}
