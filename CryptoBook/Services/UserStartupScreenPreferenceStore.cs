using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services;

public sealed class UserStartupScreenPreferenceStore:
    IStartupScreenPreferenceStore
{
    public StartupScreen Load() =>
        Properties.Settings.Default.FileOverviewFirstStart
            ? StartupScreen.Start
            : StartupScreen.Editor;

    public void Save(StartupScreen screen)
    {
        Properties.Settings.Default.FileOverviewFirstStart =
            screen == StartupScreen.Start;
        Properties.Settings.Default.Save();
    }
}
