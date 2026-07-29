namespace CryptoBook.Interfaces
{
    /// <summary>
    /// ViewModel жизненного цикла визуального контейнера RichTextBox.
    /// Редактирование и форматирование предоставляют специализированные панели.
    /// </summary>
    public interface IRichtextboxViewModel: IViewModel
    {
        bool IsPreviewMode { get; }
        string ModeLabel { get; }
        string ToggleViewText { get; }
        System.Windows.Documents.FlowDocument? PreviewDocument { get; }
        System.Windows.Input.ICommand ToggleView { get; }
        System.Windows.Input.ICommand OpenHyperlink { get; }
    }
}
