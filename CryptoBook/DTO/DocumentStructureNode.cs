using System.Windows;
using System.Windows.Documents;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CryptoBook.DTO
{
    /// <summary>
    /// Неизменяемый снимок одного узла логического дерева FlowDocument.
    /// Source остаётся ссылкой на живой WPF-элемент и перед выполнением любой
    /// команды обязательно повторно проверяется на принадлежность документу.
    /// </summary>
    public sealed class DocumentStructureNode: INotifyPropertyChanged
    {
        private bool isExpanded;
        private bool isSelected;

        public DocumentStructureNode(
            FrameworkContentElement source,
            string path,
            string typeName,
            string summary,
            string glyph,
            bool canDelete,
            IReadOnlyList<DocumentStructureNode> children,
            IReadOnlyList<FrameworkContentElement>? representedSources = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            Summary = summary ?? string.Empty;
            Glyph = glyph ?? string.Empty;
            CanDelete = canDelete;
            Children = children ?? throw new ArgumentNullException(nameof(children));
            RepresentedSources = representedSources ?? [source];
            if(RepresentedSources.Count == 0 ||
               !ReferenceEquals(RepresentedSources[0], source))
            {
                throw new ArgumentException(
                    "The primary source must be the first represented source.",
                    nameof(representedSources));
            }
        }

        public FrameworkContentElement Source { get; }
        public TextElement? Element => Source as TextElement;
        public string Path { get; }
        public string TypeName { get; }
        public string Summary { get; }
        public string Glyph { get; }
        public bool CanDelete { get; }
        public IReadOnlyList<DocumentStructureNode> Children { get; }
        public IReadOnlyList<FrameworkContentElement> RepresentedSources { get; }
        public bool IsVisualGroup => RepresentedSources.Count > 1;

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if(isExpanded == value)
                    return;
                isExpanded = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if(isSelected == value)
                    return;
                isSelected = value;
                OnPropertyChanged();
            }
        }

        public string DisplayName
        {
            get
            {
                string label = Source is Paragraph &&
                    TryGetPathIndex(Path, out int index)
                    ? $"{TypeName} {index}"
                    : TypeName;
                return string.IsNullOrWhiteSpace(Summary)
                    ? label
                    : $"{label} — {Summary}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));

        private static bool TryGetPathIndex(
            string path,
            out int index)
        {
            index = 0;
            int closingBracket = path.LastIndexOf(']');
            if(closingBracket != path.Length - 1)
                return false;

            int openingBracket = path.LastIndexOf('[', closingBracket);
            return openingBracket >= 0 &&
                closingBracket == path.Length - 1 &&
                int.TryParse(
                    path.AsSpan(
                        openingBracket + 1,
                        closingBracket - openingBracket - 1),
                    out index);
        }
    }
}
