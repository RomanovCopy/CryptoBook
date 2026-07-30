using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class DocumentFormatHandlerRegistry:
        IDocumentFormatHandlerRegistry
    {
        private readonly IReadOnlyList<IDocumentFormatHandler> handlers;

        public DocumentFormatHandlerRegistry(
            IEnumerable<IDocumentFormatHandler> handlers)
        {
            this.handlers = handlers?.ToArray()
                ?? throw new ArgumentNullException(nameof(handlers));
        }

        public IDocumentFormatHandler? Find(IFileTemplate template)
        {
            ArgumentNullException.ThrowIfNull(template);
            return handlers.FirstOrDefault(handler => handler.CanHandle(template));
        }
    }
}
