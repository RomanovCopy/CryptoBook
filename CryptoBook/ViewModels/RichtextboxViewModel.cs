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
        private readonly IFontService? fontService;
        private bool isPreviewMode;
        private bool isFitToWindow = true;
        private FlowDocument? previewDocument;

        public RichtextboxViewModel(
            IRichtextboxModel richtextboxModel,
            IRichTextBoxService richTextBox,
            IDocumentPreviewService previewService,
            IUriNavigationService uriNavigationService,
            IMenuFileViewModel menuFile,
            IFontService? fontService = null)
        {
            this.richtextboxModel = richtextboxModel ??
                throw new ArgumentNullException(nameof(richtextboxModel));
            this.richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
            this.previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
            this.uriNavigationService = uriNavigationService ??
                throw new ArgumentNullException(nameof(uriNavigationService));
            this.menuFile = menuFile
                ?? throw new ArgumentNullException(nameof(menuFile));
            this.fontService = fontService;
            richtextboxModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            LocalizationManager.CultureChanged += OnCultureChanged;
            if(fontService is not null)
            {
                fontService.DocumentBackgroundChanged +=
                    OnDocumentBackgroundChanged;
            }
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
            IsPreviewMode ? "Editor.PagedPreview" : "Editor.Editing");

        public string ToggleViewText => LocalizationManager.GetString(
            IsPreviewMode ? "Editor.Editor" : "Editor.Preview");

        public FlowDocument? PreviewDocument
        {
            get => previewDocument;
            private set => SetProperty(ref previewDocument, value);
        }

        public bool IsFitToWindow
        {
            get => isFitToWindow;
            private set
            {
                if(SetProperty(ref isFitToWindow, value))
                    OnPropertyChanged(
                        nameof(FitToWindowText),
                        nameof(FitToWindowGlyph));
            }
        }

        public string FitToWindowText => LocalizationManager.GetString(
            IsFitToWindow ? "Editor.Zoom100" : "Editor.FitToWindow");

        public string FitToWindowGlyph =>
            IsFitToWindow ? "\uE73F" : "\uE740";

        public ICommand ToggleView => toggleView ??=
            new RelayCommand(_ => SetPreviewMode(!IsPreviewMode));
        private RelayCommand? toggleView;

        public ICommand ToggleFitToWindow => toggleFitToWindow ??=
            new RelayCommand(
                _ => IsFitToWindow = !IsFitToWindow,
                _ => IsPreviewMode);
        private RelayCommand? toggleFitToWindow;

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
            new RelayCommand(
                parameter =>
                {
                    LocalizationManager.CultureChanged -= OnCultureChanged;
                    if(fontService is not null)
                    {
                        fontService.DocumentBackgroundChanged -=
                            OnDocumentBackgroundChanged;
                    }
                    richtextboxModel.Execute_Closed(parameter);
                },
                richtextboxModel.CanExecute_Closed);
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

        private void OnCultureChanged(object? sender, EventArgs args) =>
            OnPropertyChanged(
                nameof(ModeLabel),
                nameof(ToggleViewText),
                nameof(FitToWindowText));

        private void OnDocumentBackgroundChanged(
            object? sender,
            EventArgs args)
        {
            if(IsPreviewMode)
            {
                PreviewDocument = previewService.CreatePreview(
                    richTextBox.Document);
            }
        }
    }
}
