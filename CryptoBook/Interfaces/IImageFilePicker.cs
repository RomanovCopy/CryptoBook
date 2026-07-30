namespace CryptoBook.Interfaces
{
    public interface IImageFilePicker: IService
    {
        Task<string?> PickImageAsync(
            CancellationToken cancellationToken = default);
    }
}
