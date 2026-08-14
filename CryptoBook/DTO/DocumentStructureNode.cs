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

        public DocumentStructureNode(
            FrameworkContentElement source,
            string path,
            string typeName,
            string summary,
            string glyph,
            bool canDelete,
            IReadOnlyList<DocumentStructureNode> children)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            Summary = summary ?? string.Empty;
            Glyph = glyph ?? string.Empty;
            CanDelete = canDelete;
            Children = children ?? throw new ArgumentNullException(nameof(children));
        }

        public FrameworkContentElement Source { get; }
        public TextElement? Element => Source as TextElement;
        public string Path { get; }
        public string TypeName { get; }
        public string Summary { get; }
        public string Glyph { get; }
        public bool CanDelete { get; }
        public IReadOnlyList<DocumentStructureNode> Children { get; }

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

        public string DisplayName => string.IsNullOrWhiteSpace(Summary)
            ? TypeName
            : $"{TypeName} — {Summary}";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
    }
}
