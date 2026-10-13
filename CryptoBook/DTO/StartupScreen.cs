using CryptoBook.Infrastructure;

namespace CryptoBook.DTO;

public enum StartupScreen
{
    Start,
    Editor
}

public sealed class StartupScreenOption(StartupScreen screen, string displayName): ViewModelBase
{
    public StartupScreen Screen { get; } = screen;
    private string localizedDisplayName = displayName;
    public string DisplayName => localizedDisplayName;

    internal void RefreshLocalization() => SetProperty(ref localizedDisplayName,
        LocalizationManager.GetString(Screen == StartupScreen.Editor ? "Editor.Editor" : "Home.StartTitle"),
        nameof(DisplayName));
}
