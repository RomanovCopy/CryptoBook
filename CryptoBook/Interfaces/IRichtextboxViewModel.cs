namespace CryptoBook.Interfaces
{
    /// <summary>
    /// ViewModel жизненного цикла визуального контейнера RichTextBox.
    /// Редактирование и форматирование предоставляют специализированные панели.
    /// </summary>
    public interface IRichtextboxViewModel: IViewModel
    {
        bool IsPreviewMode { get; }
        bool IsFitToWindow { get; }
        string ModeLabel { get; }
        string ToggleViewText { get; }
        string FitToWindowText { get; }
        string FitToWindowGlyph { get; }
        System.Windows.Documents.FlowDocument? PreviewDocument { get; }
        System.Windows.Input.ICommand ToggleView { get; }
        System.Windows.Input.ICommand ToggleFitToWindow { get; }
        System.Windows.Input.ICommand OpenHyperlink { get; }
        System.Windows.Input.ICommand SaveDocument { get; }
        System.Windows.Input.ICommand SaveDocumentAs { get; }
    }
}
