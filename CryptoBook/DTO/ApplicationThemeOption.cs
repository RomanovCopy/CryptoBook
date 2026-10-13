using CryptoBook.Infrastructure;

namespace CryptoBook.DTO
{
    public sealed class ApplicationThemeOption(
        ApplicationTheme theme, string displayName, string description,
        string resourceName, string backgroundPreview, string accentPreview): ViewModelBase
    {
        public ApplicationTheme Theme { get; } = theme;
        public string ResourceName { get; } = resourceName;
        public string BackgroundPreview { get; } = backgroundPreview;
        public string AccentPreview { get; } = accentPreview;
        private string localizedDisplayName = displayName;
        private string localizedDescription = description;
        public string DisplayName => localizedDisplayName;
        public string Description => localizedDescription;

        internal void RefreshLocalization()
        {
            SetProperty(ref localizedDisplayName,
                LocalizationManager.GetString($"Theme.{Theme}.Name"), nameof(DisplayName));
            SetProperty(ref localizedDescription,
                LocalizationManager.GetString($"Theme.{Theme}.Description"), nameof(Description));
        }
    }
}
