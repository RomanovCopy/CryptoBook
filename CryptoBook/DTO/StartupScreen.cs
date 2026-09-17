namespace CryptoBook.DTO;

public enum StartupScreen
{
    Start,
    Editor
}

public sealed record StartupScreenOption(
    StartupScreen Screen,
    string DisplayName);
