namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Дополнительная возможность редактора: уведомляет наблюдателей, когда
    /// экземпляр FlowDocument целиком заменён при открытии другого файла.
    /// </summary>
    public interface IDocumentReplacementNotifier
    {
        event EventHandler? DocumentReplaced;
    }
}
