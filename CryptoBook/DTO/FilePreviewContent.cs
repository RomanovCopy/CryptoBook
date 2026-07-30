namespace CryptoBook.DTO
{
    public enum FilePreviewKind
    {
        Empty,
        Loading,
        Text,
        Image,
        Protected,
        Unsupported,
        Error
    }

    public sealed record FilePreviewContent(
        FilePreviewKind Kind,
        string? Text = null,
        byte[]? ImageBytes = null,
        string? Message = null);
}
