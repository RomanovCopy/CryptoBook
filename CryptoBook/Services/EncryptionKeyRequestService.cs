using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Views;

namespace CryptoBook.Services
{
    public sealed class EncryptionKeyRequestService:
        IEncryptionKeyRequestService
    {
        private readonly IKeyProvider keyProvider;
        private readonly IWindowManager windowManager;

        public EncryptionKeyRequestService(
            IKeyProvider keyProvider,
            IWindowManager windowManager)
        {
            this.keyProvider = keyProvider ??
                throw new ArgumentNullException(nameof(keyProvider));
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
        }

        public bool EnsureKeyAvailable()
        {
            if(keyProvider.HasKey)
                return true;

            return RequestKey();
        }

        public bool RequestKey()
        {
            Guid windowId = windowManager.CreateWindow<KeyInputWindow>();
            windowManager.ShowWindowDialog(windowId);
            return windowManager.GetResult<bool>(windowId) == true &&
                keyProvider.HasKey;
        }
    }
}
