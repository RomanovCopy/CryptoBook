using System.IO;
using System.Xml.Linq;

namespace CryptoBook.Infrastructure;

/// <summary>
/// Finds a previous user.config even when LocalFileSettingsProvider changed
/// the application's identity hash (for example after installing a new
/// single-file executable from another location).
/// </summary>
internal static class UserSettingsProfileMigrator
{
    private const string MigrationFlagName = "SettingsUpgradeRequired";
    private const string SettingsSectionName =
        "CryptoBook.Properties.Settings";

    public static bool TryImport(
        string currentConfigPath,
        IEnumerable<string> knownSettingNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentConfigPath);
        ArgumentNullException.ThrowIfNull(knownSettingNames);

        string? sourcePath = FindPreviousConfig(
            currentConfigPath,
            knownSettingNames);
        if(sourcePath is null)
            return false;

        string? targetDirectory = Path.GetDirectoryName(currentConfigPath);
        if(string.IsNullOrWhiteSpace(targetDirectory))
            return false;

        Directory.CreateDirectory(targetDirectory);
        string temporaryPath = Path.Combine(
            targetDirectory,
            $"user.config.migration-{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(sourcePath, temporaryPath, overwrite: false);
            File.Move(temporaryPath, currentConfigPath, overwrite: true);
            return true;
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    internal static string? FindPreviousConfig(
        string currentConfigPath,
        IEnumerable<string> knownSettingNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentConfigPath);
        ArgumentNullException.ThrowIfNull(knownSettingNames);

        string fullCurrentPath = Path.GetFullPath(currentConfigPath);
        DirectoryInfo? currentVersionDirectory =
            Directory.GetParent(fullCurrentPath);
        DirectoryInfo? currentIdentityDirectory =
            currentVersionDirectory?.Parent;
        DirectoryInfo? profileRoot = currentIdentityDirectory?.Parent;

        if(currentVersionDirectory is null ||
           currentIdentityDirectory is null ||
           profileRoot is null ||
           !Version.TryParse(currentVersionDirectory.Name, out Version? currentVersion) ||
           !profileRoot.Exists)
        {
            return null;
        }

        HashSet<string> knownNames = knownSettingNames
            .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           !string.Equals(
                               name,
                               MigrationFlagName,
                               StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        if(knownNames.Count == 0)
            return null;

        string identityPrefix = GetIdentityPrefix(currentIdentityDirectory.Name);
        var candidates = new List<ProfileCandidate>();

        foreach(DirectoryInfo identityDirectory in profileRoot.EnumerateDirectories())
        {
            if(!identityDirectory.Name.StartsWith(
                identityPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach(DirectoryInfo versionDirectory in
                identityDirectory.EnumerateDirectories())
            {
                if(!Version.TryParse(versionDirectory.Name, out Version? version) ||
                   version > currentVersion)
                {
                    continue;
                }

                string candidatePath = Path.Combine(
                    versionDirectory.FullName,
                    "user.config");
                if(string.Equals(
                    Path.GetFullPath(candidatePath),
                    fullCurrentPath,
                    StringComparison.OrdinalIgnoreCase) ||
                   !File.Exists(candidatePath))
                {
                    continue;
                }

                int transferableSettingCount = CountTransferableSettings(
                    candidatePath,
                    knownNames);
                if(transferableSettingCount == 0)
                    continue;

                candidates.Add(new ProfileCandidate(
                    candidatePath,
                    version,
                    transferableSettingCount,
                    File.GetLastWriteTimeUtc(candidatePath)));
            }
        }

        // A failed framework migration can leave a newer but nearly empty
        // profile behind. Prefer the profile containing the most usable
        // settings; version and write time resolve ties between full profiles.
        return candidates
            .OrderByDescending(candidate => candidate.TransferableSettingCount)
            .ThenByDescending(candidate => candidate.Version)
            .ThenByDescending(candidate => candidate.LastWriteTimeUtc)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    private static int CountTransferableSettings(
        string configPath,
        IReadOnlySet<string> knownSettingNames)
    {
        try
        {
            XDocument document = XDocument.Load(
                configPath,
                LoadOptions.None);
            XElement? settingsSection = document
                .Descendants()
                .FirstOrDefault(element =>
                    string.Equals(
                        element.Name.LocalName,
                        SettingsSectionName,
                        StringComparison.Ordinal));
            if(settingsSection is null)
                return 0;

            return settingsSection
                .Elements()
                .Where(element =>
                    string.Equals(
                        element.Name.LocalName,
                        "setting",
                        StringComparison.Ordinal))
                .Select(element => (string?)element.Attribute("name"))
                .Where(name => name is not null && knownSettingNames.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .Count();
        }
        catch(IOException)
        {
            return 0;
        }
        catch(UnauthorizedAccessException)
        {
            return 0;
        }
        catch(System.Xml.XmlException)
        {
            return 0;
        }
    }

    private static string GetIdentityPrefix(string identityDirectoryName)
    {
        int separatorIndex = identityDirectoryName.IndexOf('_');
        return separatorIndex > 0
            ? identityDirectoryName[..(separatorIndex + 1)]
            : identityDirectoryName;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if(File.Exists(path))
                File.Delete(path);
        }
        catch(IOException)
        {
            // The copied target is already in place; a stale temporary file
            // must not turn a successful migration into a startup failure.
        }
        catch(UnauthorizedAccessException)
        {
            // See the IOException comment above.
        }
    }

    private sealed record ProfileCandidate(
        string Path,
        Version Version,
        int TransferableSettingCount,
        DateTime LastWriteTimeUtc);
}
