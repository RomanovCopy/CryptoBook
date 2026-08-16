using CryptoBook.Infrastructure;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class UserSettingsMigratorTests
{
    [Fact]
    public void MigrateIfRequired_UpgradesBeforeCompletingMigration()
    {
        var store = new RecordingMigrationStore(upgradeRequired: true);

        bool migrated = UserSettingsMigrator.MigrateIfRequired(store);

        Assert.True(migrated);
        Assert.False(store.UpgradeRequired);
        Assert.Equal(["Upgrade", "Complete", "Save"], store.Calls);
    }

    [Fact]
    public void MigrateIfRequired_WhenAlreadyCompleted_DoesNothing()
    {
        var store = new RecordingMigrationStore(upgradeRequired: false);

        bool migrated = UserSettingsMigrator.MigrateIfRequired(store);

        Assert.False(migrated);
        Assert.Empty(store.Calls);
    }

    [Fact]
    public void MigrateIfRequired_WhenUpgradeFails_KeepsMigrationPending()
    {
        var store = new RecordingMigrationStore(
            upgradeRequired: true,
            upgradeException: new InvalidOperationException("Upgrade failed."));

        Assert.Throws<InvalidOperationException>(
            () => UserSettingsMigrator.MigrateIfRequired(store));

        Assert.True(store.UpgradeRequired);
        Assert.Equal(["Upgrade"], store.Calls);
    }

    [Fact]
    public void ProfileMigration_WhenIdentityHashChanged_ImportsMostCompleteProfile()
    {
        using var directory = new TemporaryDirectory();
        string currentConfig = directory.GetConfigPath(
            "CryptoBook_Url_current",
            "1.2.0.0");
        string completeConfig = directory.WriteConfig(
            "CryptoBook_Url_complete",
            "1.1.0.0",
            ("CultureInfo", "ru-RU"),
            ("WorkspaceDirectory", @"C:\Books"));
        directory.WriteConfig(
            "CryptoBook_Url_incomplete",
            "1.1.1.0",
            ("CultureInfo", "en-US"));
        directory.WriteConfig(
            "CryptoBook_Url_future",
            "2.0.0.0",
            ("CultureInfo", "en-US"),
            ("WorkspaceDirectory", @"C:\Future"),
            ("CurrentTheme", "Dark"));

        bool imported = UserSettingsProfileMigrator.TryImport(
            currentConfig,
            ["CultureInfo", "WorkspaceDirectory", "CurrentTheme"]);

        Assert.True(imported);
        Assert.Equal(
            File.ReadAllText(completeConfig),
            File.ReadAllText(currentConfig));
    }

    [Fact]
    public void ProfileMigration_WhenSameVersionHasAnotherHash_ImportsIt()
    {
        using var directory = new TemporaryDirectory();
        string currentConfig = directory.GetConfigPath(
            "CryptoBook_Url_new",
            "1.1.1.6");
        string previousConfig = directory.WriteConfig(
            "CryptoBook_Url_old",
            "1.1.1.6",
            ("CurrentTheme", "Dark"));

        bool imported = UserSettingsProfileMigrator.TryImport(
            currentConfig,
            ["CurrentTheme"]);

        Assert.True(imported);
        Assert.Equal(
            File.ReadAllText(previousConfig),
            File.ReadAllText(currentConfig));
    }

    [Fact]
    public void ProfileMigration_WhenNoCompatibleProfile_LeavesCurrentFileUntouched()
    {
        using var directory = new TemporaryDirectory();
        string currentConfig = directory.WriteConfig(
            "CryptoBook_Url_current",
            "1.2.0.0",
            ("CultureInfo", "ru-RU"));
        directory.WriteConfig(
            "AnotherProduct_Url_old",
            "1.1.0.0",
            ("CultureInfo", "en-US"));

        bool imported = UserSettingsProfileMigrator.TryImport(
            currentConfig,
            ["CultureInfo"]);

        Assert.False(imported);
        Assert.Contains("ru-RU", File.ReadAllText(currentConfig));
    }

    private sealed class RecordingMigrationStore(
        bool upgradeRequired,
        Exception? upgradeException = null): IUserSettingsMigrationStore
    {
        private bool upgradeRequired = upgradeRequired;

        public List<string> Calls { get; } = [];

        public bool UpgradeRequired
        {
            get => upgradeRequired;
            set
            {
                upgradeRequired = value;
                Calls.Add("Complete");
            }
        }

        public void Upgrade()
        {
            Calls.Add("Upgrade");
            if(upgradeException is not null)
                throw upgradeException;
        }

        public void Save() => Calls.Add("Save");
    }

    private sealed class TemporaryDirectory: IDisposable
    {
        private readonly string path = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));

        public string GetConfigPath(string identity, string version) =>
            Path.Combine(path, identity, version, "user.config");

        public string WriteConfig(
            string identity,
            string version,
            params (string Name, string Value)[] settings)
        {
            string configPath = GetConfigPath(identity, version);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var settingsXml = string.Join(
                Environment.NewLine,
                settings.Select(setting =>
                    $"<setting name=\"{setting.Name}\" serializeAs=\"String\"><value>{setting.Value}</value></setting>"));
            File.WriteAllText(
                configPath,
                $"<configuration><userSettings><CryptoBook.Properties.Settings>{settingsXml}</CryptoBook.Properties.Settings></userSettings></configuration>");
            return configPath;
        }

        public void Dispose()
        {
            if(Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }
}
