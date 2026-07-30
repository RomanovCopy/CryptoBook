namespace CryptoBook.Interfaces
{
    public interface IWindowCaptionProvider
    {
        string Caption { get; }
        string? CaptionToolTip { get; }
    }
}
