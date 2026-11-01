using CryptoBook.Infrastructure;

namespace CryptoBook.DTO;

public sealed class KeyResetIntervalOption(int minutes, string displayName) : ViewModelBase
{
    public int Minutes { get; } = minutes;
    private string localizedDisplayName = displayName;
    public string DisplayName => localizedDisplayName;
    internal void RefreshLocalization() => SetProperty(ref localizedDisplayName,
        LocalizationManager.GetString($"Security.ResetInterval.{Minutes}"), nameof(DisplayName));

    public static IReadOnlyList<KeyResetIntervalOption> All { get; } =
    [
        new(1, LocalizationManager.GetString("Security.ResetInterval.1")),
        new(5, LocalizationManager.GetString("Security.ResetInterval.5")),
        new(10, LocalizationManager.GetString("Security.ResetInterval.10")),
        new(15, LocalizationManager.GetString("Security.ResetInterval.15")),
        new(30, LocalizationManager.GetString("Security.ResetInterval.30")),
        new(0, LocalizationManager.GetString("Security.ResetInterval.0"))
    ];
}
