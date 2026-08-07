namespace CryptoBook.Infrastructure;

/// <summary>
/// Однократно переносит пользовательские настройки из профиля предыдущей
/// версии до того, как приложение впервые прочитает их при запуске.
/// </summary>
public static class UserSettingsMigrator
{
    public static void MigrateIfRequired() =>
        MigrateIfRequired(new UserSettingsMigrationStore());

    internal static bool MigrateIfRequired(
        IUserSettingsMigrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        if(!store.UpgradeRequired)
            return false;

        store.Upgrade();
        store.UpgradeRequired = false;
        store.Save();
        return true;
    }
}

internal interface IUserSettingsMigrationStore
{
    bool UpgradeRequired { get; set; }

    void Upgrade();

    void Save();
}

internal sealed class UserSettingsMigrationStore: IUserSettingsMigrationStore
{
    private Properties.Settings Settings => Properties.Settings.Default;

    public bool UpgradeRequired
    {
        get => Settings.SettingsUpgradeRequired;
        set => Settings.SettingsUpgradeRequired = value;
    }

    public void Upgrade() => Settings.Upgrade();

    public void Save() => Settings.Save();
}
