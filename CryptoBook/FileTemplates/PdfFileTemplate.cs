using CryptoBook.Interfaces;

namespace CryptoBook.FileTemplates
{
    /// <summary>
    /// Описывает PDF как внешний, доступный только для открытия формат.
    /// CryptoBook не создаёт и не загружает его в FlowDocument.
    /// </summary>
    public sealed class PdfFileTemplate: IFileTemplate
    {
        public string Id => "Pdf";
        public string DisplayName => "PDF";
        public string DefaultExtension => ".pdf";
        public IReadOnlyCollection<string> Extensions => [".pdf"];
        public string SuggestedBaseName => "Document";
        public bool CanCreate => false;
        public FileOpenMode OpenMode => FileOpenMode.External;

        public Task<byte[]> GetInitialContentAsync(CancellationToken ct) =>
            throw new NotSupportedException(
                "Создание PDF в CryptoBook не поддерживается.");
    }
}
