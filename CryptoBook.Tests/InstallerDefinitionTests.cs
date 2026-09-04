using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class InstallerDefinitionTests
{
    private static readonly string[] SupportedInstallerLanguages =
    [
        "Name: \"english\"; MessagesFile: \"compiler:Default.isl\"",
        "Name: \"german\"; MessagesFile: \"compiler:Languages\\German.isl\"",
        "Name: \"russian\"; MessagesFile: \"compiler:Languages\\Russian.isl\"",
        "Name: \"ukrainian\"; MessagesFile: \"compiler:Languages\\Ukrainian.isl\""
    ];

    private static readonly string[] LegacyRuntimeCleanupEntries =
    [
        "Type: files; Name: \"{app}\\*.dll\"",
        "Type: files; Name: \"{app}\\*.deps.json\"",
        "Type: files; Name: \"{app}\\*.runtimeconfig.json\"",
        "Type: files; Name: \"{app}\\*.config\"",
        "Type: files; Name: \"{app}\\createdump.exe\"",
        "Type: filesandordirs; Name: \"{app}\\ru\"",
        "Type: filesandordirs; Name: \"{app}\\uk\"",
        "Type: filesandordirs; Name: \"{app}\\runtimes\"",
        "Type: filesandordirs; Name: \"{app}\\LICENSES\"",
        "Type: filesandordirs; Name: \"{app}\\compliance\""
    ];

    [Fact]
    public void Installer_RemovesVersionedIconsBeforeCopyingCurrentIcon()
    {
        string installer = File.ReadAllText(FindRepositoryFile(
            "installer",
            "CryptoBook.iss"));

        int cleanupSection = installer.IndexOf(
            "[InstallDelete]",
            StringComparison.Ordinal);
        int cleanupEntry = installer.IndexOf(
            "Type: files; Name: \"{app}\\CryptoBook-*.ico\"",
            StringComparison.Ordinal);
        int filesSection = installer.IndexOf(
            "[Files]",
            StringComparison.Ordinal);

        Assert.True(cleanupSection >= 0);
        Assert.True(cleanupEntry > cleanupSection);
        Assert.True(filesSection > cleanupEntry);
        Assert.Contains(
            "DestName: \"{#MyShortcutIconName}\"",
            installer,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_RemovesLegacyMultiFileRuntimeBeforeCopyingSingleFile()
    {
        string installer = File.ReadAllText(FindRepositoryFile(
            "installer",
            "CryptoBook.iss"));

        int cleanupSection = installer.IndexOf(
            "[InstallDelete]",
            StringComparison.Ordinal);
        int filesSection = installer.IndexOf(
            "[Files]",
            StringComparison.Ordinal);
        Assert.True(cleanupSection >= 0);
        Assert.True(filesSection > cleanupSection);

        string cleanupRules = installer[cleanupSection..filesSection];
        foreach(string entry in LegacyRuntimeCleanupEntries)
        {
            Assert.Contains(entry, cleanupRules, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            "Type: filesandordirs; Name: \"{app}\\*\"",
            cleanupRules,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ProvidesEverySupportedApplicationLanguage()
    {
        string installer = File.ReadAllText(FindRepositoryFile(
            "installer",
            "CryptoBook.iss"));

        foreach(string language in SupportedInstallerLanguages)
            Assert.Contains(language, installer, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while(directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. parts]);
            if(File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(
            Path.DirectorySeparatorChar,
            parts));
    }
}
