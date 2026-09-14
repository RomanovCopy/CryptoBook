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
        private readonly Lazy<IKeyResetService>? keyResetService;

        public EncryptionKeyRequestService(
            IKeyProvider keyProvider,
            IWindowManager windowManager,
            Lazy<IKeyResetService>? keyResetService = null)
        {
            this.keyProvider = keyProvider ??
                throw new ArgumentNullException(nameof(keyProvider));
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
            this.keyResetService = keyResetService;
        }

        public bool EnsureKeyAvailable()
        {
            if(!KeyEntryAllowed) return false;
            if(keyProvider.HasKey)
                return true;

            return RequestKey();
        }

        public bool RequestKey()
            => RequestKey(forDecryption: true);

        public bool EnsureEncryptionKeyAvailable() => KeyEntryAllowed && (keyProvider.CanEncrypt ||
            (RequestKey(forDecryption: false) && keyProvider.CanEncrypt));

        private bool KeyEntryAllowed => keyResetService?.Value.State is not
            (KeyResetState.Resetting or KeyResetState.Unlocking) &&
            !(keyResetService?.Value.State == KeyResetState.KeyReset &&
              keyResetService.Value.HasRetainedDocument);

        private bool RequestKey(bool forDecryption)
        {
            if(!KeyEntryAllowed) return false;
            Guid windowId = windowManager.CreateWindow<KeyInputWindow>(
                new Dictionary<string, object?> { ["ForDecryption"] = forDecryption });
            windowManager.ShowWindowDialog(windowId);
            return windowManager.GetResult<bool>(windowId) == true &&
                keyProvider.HasKey;
        }
    }
}
