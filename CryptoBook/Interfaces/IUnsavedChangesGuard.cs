namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Разрешает продолжить операцию только после обработки несохранённых
    /// изменений текущего документа.
    /// </summary>
    public interface IUnsavedChangesGuard: IService
    {
        Task<bool> CanProceedAsync(
            CancellationToken cancellationToken = default);

        Task<bool> CanCloseAsync(
            CancellationToken cancellationToken = default) =>
            CanProceedAsync(cancellationToken);
    }
}
