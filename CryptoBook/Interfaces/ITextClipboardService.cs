namespace CryptoBook.Interfaces;

public interface ITextClipboardService : IService
{
    bool TrySetText(string text);
}
