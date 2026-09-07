using System.ComponentModel;
using System.Windows.Documents;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMarkdownEditorViewModel:
        IPageViewModel,
        INotifyPropertyChanged
    {
        string MarkdownText { get; set; }
        bool IsPreviewMode { get; }
        string ModeLabel { get; }
        string ToggleViewText { get; }
        FlowDocument? PreviewDocument { get; }
        ICommand ToggleView { get; }
        ICommand OpenHyperlink { get; }
        ICommand SaveDocument { get; }
        ICommand SaveDocumentAs { get; }
    }
}
