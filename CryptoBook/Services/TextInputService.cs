using CryptoBook.Interfaces;
using CryptoBook.Views;

namespace CryptoBook.Services
{
    public sealed class TextInputService: ITextInputService
    {
        private readonly IWindowManager _windowManager;

        public TextInputService(IWindowManager windowManager)
        {
            _windowManager = windowManager
                ?? throw new ArgumentNullException(nameof(windowManager));
        }

        public string? Request(
            string title,
            string prompt,
            string initialValue,
            string acceptButtonText)
        {
            var arguments = new Dictionary<string, object?>
            {
                ["title"] = title,
                ["prompt"] = prompt,
                ["initialValue"] = initialValue,
                ["acceptButtonText"] = acceptButtonText
            };
            Guid id = _windowManager.CreateWindow<TextInputDialog>(arguments);
            _windowManager.ShowWindowDialog(id);
            return _windowManager.GetResult<string?>(id);
        }
    }
}
