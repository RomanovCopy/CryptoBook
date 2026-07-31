using CryptoBook.Interfaces;
using CryptoBook.Views;

namespace CryptoBook.Services
{
    public sealed class SettingsWindowService: ISettingsWindowService
    {
        private readonly IWindowManager windowManager;
        private Guid? settingsWindowId;

        public SettingsWindowService(IWindowManager windowManager)
        {
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
        }

        public void Open()
        {
            if(settingsWindowId is Guid existingId &&
               windowManager.IsWindowOpen(existingId))
            {
                windowManager.ActivateWindow(existingId);
                return;
            }

            settingsWindowId =
                windowManager.CreateWindow<SettingsWindow>();
            windowManager.ShowWindow(settingsWindowId.Value);
        }
    }
}
