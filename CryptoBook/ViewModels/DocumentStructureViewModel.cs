using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

using WpfImage = System.Windows.Controls.Image;

namespace CryptoBook.ViewModels
{
    /// <summary>
    /// Синхронизирует раскрываемую панель структуры с текущим FlowDocument и
    /// выполняет навигацию/удаление только после проверки живой WPF-ссылки.
    /// </summary>
    public sealed class DocumentStructureViewModel:
        ViewModelBase,
        IDocumentStructureViewModel
    {
        private static readonly TimeSpan RefreshDelay =
            TimeSpan.FromMilliseconds(250);

        private readonly IFlowDocumentStructureBuilder structureBuilder;
        private readonly IFlowDocumentWalker documentWalker;
        private readonly IRichTextBoxService richTextBox;
        private readonly IRichtextboxViewModel documentView;
        private readonly IEmbeddedImageLayoutService imageLayoutService;
        private readonly IBookmarkService bookmarkService;
        private readonly IDocumentSession documentSession;
        private readonly IParagraphFactory paragraphFactory;
        private readonly IMessageService messageService;
        private readonly DispatcherTimer refreshTimer;
        private readonly IDocumentReplacementNotifier? documentNotifier;
        private readonly RelayCommand toggleCommand;
        private readonly RelayCommand refreshCommand;
        private readonly RelayCommand navigateCommand;
        private readonly AsyncRelayCommand deleteCommand;
        private readonly RelayCommand noOperationCommand = new(_ => { });
        private bool isOpen;
        private bool includeTextElements;
        private bool hasNodes;

        public DocumentStructureViewModel(
            IFlowDocumentStructureBuilder structureBuilder,
            IFlowDocumentWalker documentWalker,
            IRichTextBoxService richTextBox,
            IRichtextboxViewModel documentView,
            IEmbeddedImageLayoutService imageLayoutService,
            IBookmarkService bookmarkService,
            IDocumentSession documentSession,
            IParagraphFactory paragraphFactory,
            IMessageService messageService)
        {
            this.structureBuilder = structureBuilder ??
                throw new ArgumentNullException(nameof(structureBuilder));
            this.documentWalker = documentWalker ??
                throw new ArgumentNullException(nameof(documentWalker));
            this.richTextBox = richTextBox ??
                throw new ArgumentNullException(nameof(richTextBox));
            this.documentView = documentView ??
                throw new ArgumentNullException(nameof(documentView));
            this.imageLayoutService = imageLayoutService ??
                throw new ArgumentNullException(nameof(imageLayoutService));
            this.bookmarkService = bookmarkService ??
                throw new ArgumentNullException(nameof(bookmarkService));
            this.documentSession = documentSession ??
                throw new ArgumentNullException(nameof(documentSession));
            this.paragraphFactory = paragraphFactory ??
                throw new ArgumentNullException(nameof(paragraphFactory));
            this.messageService = messageService ??
                throw new ArgumentNullException(nameof(messageService));

            toggleCommand = new RelayCommand(
                _ => IsOpen = !IsOpen,
                _ => documentSession.HasDocument);
            refreshCommand = new RelayCommand(
                _ => RefreshNow(),
                _ => IsOpen && documentSession.HasDocument);
            navigateCommand = new RelayCommand(
                NavigateTo,
                CanNavigateTo);
            deleteCommand = new AsyncRelayCommand(
                DeleteAsync,
                CanDelete);

            refreshTimer = new DispatcherTimer(
                RefreshDelay,
                DispatcherPriority.Background,
                OnRefreshTimerTick,
                richTextBox.Service.Dispatcher);

            richTextBox.Service.TextChanged += OnDocumentTextChanged;
            documentSession.PropertyChanged += OnDocumentSessionChanged;
            documentView.PropertyChanged += OnDocumentViewChanged;

            documentNotifier = richTextBox as IDocumentReplacementNotifier;
            if(documentNotifier is not null)
                documentNotifier.DocumentReplaced += OnDocumentReplaced;
        }

        public ObservableCollection<DocumentStructureNode> Nodes { get; } = [];

        public bool IsOpen
        {
            get => isOpen;
            private set
            {
                if(!SetProperty(ref isOpen, value))
                    return;

                if(value)
                    RefreshNow();
                else
                {
                    refreshTimer.Stop();
                    Nodes.Clear();
                    HasNodes = false;
                }

                RaiseCommandStates();
            }
        }

        public bool IncludeTextElements
        {
            get => includeTextElements;
            set
            {
                if(SetProperty(ref includeTextElements, value) && IsOpen)
                    RefreshNow();
            }
        }

        public bool HasNodes
        {
            get => hasNodes;
            private set => SetProperty(ref hasNodes, value);
        }

        public bool IsEditingEnabled =>
            !documentView.IsPreviewMode &&
            !richTextBox.IsReadOnly;

        public ICommand ToggleCommand => toggleCommand;
        public ICommand RefreshCommand => refreshCommand;
        public ICommand NavigateCommand => navigateCommand;
        public ICommand DeleteCommand => deleteCommand;

        public ICommand Loaded => noOperationCommand;
        public ICommand Close => noOperationCommand;
        public ICommand Closing => noOperationCommand;
        public ICommand Closed => noOperationCommand;

        private void RefreshNow()
        {
            refreshTimer.Stop();
            bool hadSnapshot = Nodes.Count > 0;
            HashSet<string> expandedPaths = Nodes
                .SelectMany(Flatten)
                .Where(node => node.IsExpanded)
                .Select(node => node.Path)
                .ToHashSet(StringComparer.Ordinal);
            Nodes.Clear();

            if(!IsOpen || !documentSession.HasDocument)
            {
                HasNodes = false;
                return;
            }

            DocumentStructureNode root = structureBuilder.Build(
                richTextBox.Document,
                IncludeTextElements);
            foreach(DocumentStructureNode node in Flatten(root))
            {
                node.IsExpanded = expandedPaths.Contains(node.Path) ||
                    !hadSnapshot && ReferenceEquals(node, root);
            }
            Nodes.Add(root);
            HasNodes = root.Children.Count > 0;
            RaiseCommandStates();
        }

        private void ScheduleRefresh()
        {
            Dispatcher dispatcher = richTextBox.Service.Dispatcher;
            if(!dispatcher.CheckAccess())
            {
                _ = dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(ScheduleRefresh));
                return;
            }

            if(!IsOpen)
                return;

            refreshTimer.Stop();
            refreshTimer.Start();
        }

        private void OnRefreshTimerTick(object? sender, EventArgs args) =>
            RefreshNow();

        private void OnDocumentTextChanged(
            object sender,
            TextChangedEventArgs args) =>
            ScheduleRefresh();

        private void OnDocumentReplaced(object? sender, EventArgs args)
        {
            if(IsOpen)
                RefreshNow();
        }

        private void OnDocumentSessionChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName != nameof(IDocumentSession.HasDocument))
                return;

            if(!documentSession.HasDocument)
            {
                IsOpen = false;
                Nodes.Clear();
                HasNodes = false;
            }
            else
            {
                ScheduleRefresh();
            }

            toggleCommand.RaiseCanExecuteChanged();
        }

        private void OnDocumentViewChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName != nameof(IRichtextboxViewModel.IsPreviewMode))
                return;

            OnPropertyChanged(nameof(IsEditingEnabled));
            RaiseCommandStates();
        }

        private bool CanNavigateTo(object? parameter) =>
            parameter is DocumentStructureNode node &&
            IsEditingEnabled &&
            IsAttached(node.Source);

        private void NavigateTo(object? parameter)
        {
            if(parameter is not DocumentStructureNode node ||
               !CanNavigateTo(node))
            {
                return;
            }

            TextPointer position = node.Element?.ContentStart ??
                richTextBox.Document.ContentStart;
            TextPointer insertion =
                position.GetInsertionPosition(LogicalDirection.Forward) ??
                position.GetInsertionPosition(LogicalDirection.Backward) ??
                richTextBox.Document.ContentStart;

            richTextBox.CaretPosition = insertion;
            richTextBox.Selection.Select(insertion, insertion);
            richTextBox.Focus();
            richTextBox.ScrollToCaret();
        }

        private bool CanDelete(object? parameter) =>
            parameter is DocumentStructureNode
            {
                CanDelete: true,
                Element: not null
            } node &&
            IsEditingEnabled &&
            IsAttached(node.Source);

        private async Task DeleteAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            if(parameter is not DocumentStructureNode node ||
               !CanDelete(node))
            {
                return;
            }

            Guid dialogId = await messageService.ShowMessage(
                LocalizationManager.GetString(
                    "Editor.DocumentStructureDeleteTitle"),
                LocalizationManager.Format(
                    "Editor.DocumentStructureDeletePrompt",
                    node.DisplayName),
                isCanceled: true);
            cancellationToken.ThrowIfCancellationRequested();
            if(!messageService.ShowConfirmation(dialogId) ||
               node.Element is not TextElement element ||
               !IsAttached(element))
            {
                return;
            }

            RemoveElement(element);
        }

        private void RemoveElement(TextElement element)
        {
            FlowDocument document = richTextBox.Document;
            DependencyObject? owner = element.Parent;
            int caretOffset = GetElementOffset(document, element);
            long revisionBefore = documentSession.Revision;
            bool removed;

            if(element is Block block &&
               ReferenceEquals(block.Parent, document) &&
               document.Blocks.Count == 1)
            {
                richTextBox.ClearDocument();
                removed = true;
            }
            else
            {
                richTextBox.BeginChange();
                try
                {
                    WpfImage? image = FindOwnedImage(element);
                    removed = image is not null &&
                        imageLayoutService.Remove(image);
                    if(!removed)
                        removed = documentWalker.Remove(element);

                    if(removed)
                        EnsureEditableOwner(owner, document);
                }
                finally
                {
                    richTextBox.EndChange();
                }
            }

            if(!removed)
                return;

            bookmarkService.RebuildIndexFromDocument(richTextBox);
            if(documentSession.Revision == revisionBefore)
                documentSession.MarkDirty();

            RestoreCaret(document, caretOffset);
            RefreshNow();
        }

        private void EnsureEditableOwner(
            DependencyObject? owner,
            FlowDocument document)
        {
            switch(owner)
            {
                case ListItem item when item.Blocks.Count == 0:
                    item.Blocks.Add(CreateParagraph());
                    break;

                case TableCell cell when cell.Blocks.Count == 0:
                    cell.Blocks.Add(CreateParagraph());
                    break;

                case Section section when section.Blocks.Count == 0:
                    section.Blocks.Add(CreateParagraph());
                    break;

                case AnchoredBlock anchoredBlock
                    when anchoredBlock.Blocks.Count == 0:
                {
                    DependencyObject? parent = anchoredBlock.Parent;
                    if(documentWalker.Remove(anchoredBlock))
                        EnsureEditableOwner(parent, document);
                    break;
                }

                case System.Windows.Documents.List list
                    when list.ListItems.Count == 0:
                {
                    DependencyObject? parent = list.Parent;
                    if(documentWalker.Remove(list))
                        EnsureEditableOwner(parent, document);
                    break;
                }

                case TableRow row when row.Cells.Count == 0:
                {
                    DependencyObject? parent = row.Parent;
                    if(documentWalker.Remove(row))
                        EnsureEditableOwner(parent, document);
                    break;
                }

                case TableRowGroup group when group.Rows.Count == 0:
                {
                    DependencyObject? parent = group.Parent;
                    if(documentWalker.Remove(group))
                        EnsureEditableOwner(parent, document);
                    break;
                }

                case Table table when table.RowGroups.Count == 0:
                {
                    DependencyObject? parent = table.Parent;
                    if(documentWalker.Remove(table))
                        EnsureEditableOwner(parent, document);
                    break;
                }

                case FlowDocument when document.Blocks.Count == 0:
                    document.Blocks.Add(CreateParagraph());
                    break;
            }
        }

        private Paragraph CreateParagraph()
        {
            IParagraphService paragraph = paragraphFactory.Create();
            paragraph.Margin = new Thickness(0);
            paragraph.Element.ClearValue(Paragraph.LineHeightProperty);
            paragraph.LineStackingStrategy =
                LineStackingStrategy.MaxHeight;
            return paragraph.Element;
        }

        private void RestoreCaret(FlowDocument document, int offset)
        {
            int documentLength = document.ContentStart.GetOffsetToPosition(
                document.ContentEnd);
            int targetOffset = Math.Clamp(offset, 0, documentLength);
            TextPointer? position = document.ContentStart.GetPositionAtOffset(
                targetOffset,
                LogicalDirection.Forward);
            TextPointer insertion =
                position?.GetInsertionPosition(LogicalDirection.Forward) ??
                position?.GetInsertionPosition(LogicalDirection.Backward) ??
                document.ContentStart.GetInsertionPosition(
                    LogicalDirection.Forward) ??
                document.ContentStart;

            richTextBox.CaretPosition = insertion;
            richTextBox.Selection.Select(insertion, insertion);
            richTextBox.Focus();
            richTextBox.ScrollToCaret();
        }

        private static int GetElementOffset(
            FlowDocument document,
            TextElement element)
        {
            try
            {
                return document.ContentStart.GetOffsetToPosition(
                    element.ElementStart);
            }
            catch(InvalidOperationException)
            {
                return 0;
            }
        }

        private bool IsAttached(FrameworkContentElement source)
        {
            DependencyObject? current = source;
            while(current is FrameworkContentElement element)
            {
                if(ReferenceEquals(element, richTextBox.Document))
                    return true;
                current = element.Parent;
            }

            return false;
        }

        private static WpfImage? FindOwnedImage(TextElement element) =>
            element switch
            {
                InlineUIContainer { Child: WpfImage image } => image,
                BlockUIContainer { Child: WpfImage image } => image,
                Figure figure => FindImage(figure.Blocks),
                _ => null
            };

        private static WpfImage? FindImage(BlockCollection blocks)
        {
            foreach(Block block in blocks)
            {
                if(block is BlockUIContainer { Child: WpfImage image })
                    return image;

                if(block is Section section)
                {
                    WpfImage? nested = FindImage(section.Blocks);
                    if(nested is not null)
                        return nested;
                }
            }

            return null;
        }

        private static IEnumerable<DocumentStructureNode> Flatten(
            DocumentStructureNode root)
        {
            yield return root;
            foreach(DocumentStructureNode child in root.Children)
            {
                foreach(DocumentStructureNode descendant in Flatten(child))
                    yield return descendant;
            }
        }

        private void RaiseCommandStates()
        {
            refreshCommand.RaiseCanExecuteChanged();
            navigateCommand.RaiseCanExecuteChanged();
            deleteCommand.RaiseCanExecuteChanged();
        }
    }
}
