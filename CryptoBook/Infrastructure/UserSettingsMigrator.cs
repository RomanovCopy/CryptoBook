namespace CryptoBook.Infrastructure;

using System.Configuration;

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

    public void Upgrade()
    {
        string currentConfigPath = ConfigurationManager
            .OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
            .FilePath;
        string[] settingNames = Settings.Properties
            .Cast<SettingsProperty>()
            .Select(property => property.Name)
            .ToArray();

        if(UserSettingsProfileMigrator.TryImport(
            currentConfigPath,
            settingNames))
        {
            // Settings.Default was already initialized when UpgradeRequired was
            // read. Reload it so the values copied from another identity hash
            // become visible before the application reads any preferences.
            Settings.Reload();
            return;
        }

        // Keep the framework migration as a fallback for non-standard hosts
        // whose profile directory does not follow the normal version layout.
        Settings.Upgrade();
    }

    public void Save() => Settings.Save();
}
