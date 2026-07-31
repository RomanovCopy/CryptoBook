namespace CryptoBook.Interfaces
{
    public interface ITextInputService: IService
    {
        string? Request(
            string title,
            string prompt,
            string initialValue,
            string acceptButtonText);
    }
}
