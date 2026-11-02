using System.Windows.Input;

namespace CryptoBook.Infrastructure;

public static class RecoveryCommands
{
    public static RoutedUICommand RestoreCrashSnapshot { get; } = new(
        "RestoreCrashSnapshot", nameof(RestoreCrashSnapshot), typeof(RecoveryCommands));
    public static RoutedUICommand RestoreLockSnapshot { get; } = new(
        "RestoreLockSnapshot", nameof(RestoreLockSnapshot), typeof(RecoveryCommands));
}
