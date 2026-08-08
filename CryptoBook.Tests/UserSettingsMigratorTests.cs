using CryptoBook.Infrastructure;

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
}
