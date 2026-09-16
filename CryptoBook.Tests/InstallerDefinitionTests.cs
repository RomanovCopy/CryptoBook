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
    public void Installer_UsesStableExecutableIconForShellIntegration()
    {
        string installerPath = FindRepositoryFile(
            "installer",
            "CryptoBook.iss");
        string installer = File.ReadAllText(installerPath);

        string[] defaultIconLines = File.ReadAllLines(installerPath)
            .Where(line => line.Contains(
                "\\DefaultIcon\"",
                StringComparison.Ordinal))
            .ToArray();

        Assert.DoesNotContain("MyShortcutIconName", installer, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Source: \"..\\CryptoBook\\Resources\\Icons\\AppIcon.ico\"; DestDir: \"{app}\"",
            installer,
            StringComparison.Ordinal);
        Assert.Contains(
            "Name: \"{group}\\{#MyAppName}\"; Filename: \"{app}\\{#MyAppExeName}\"; IconFilename: \"{app}\\{#MyAppExeName}\"",
            installer,
            StringComparison.Ordinal);
        Assert.Contains(
            "Name: \"{autodesktop}\\{#MyAppName}\"; Filename: \"{app}\\{#MyAppExeName}\"; IconFilename: \"{app}\\{#MyAppExeName}\"; Tasks: desktopicon",
            installer,
            StringComparison.Ordinal);
        Assert.Equal(2, defaultIconLines.Length);
        Assert.All(defaultIconLines, line => Assert.Contains(
            "ValueData: \"{app}\\{#MyAppExeName},0\"",
            line,
            StringComparison.Ordinal));
    }

    [Fact]
    public void Installer_MigratesPinnedShortcutBeforeRemovingVersionedIcons()
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
        string declarativeCleanup = installer[cleanupSection..filesSection];

        Assert.DoesNotContain(
            "CryptoBook-*.ico",
            declarativeCleanup,
            StringComparison.Ordinal);
        Assert.Contains(
            "function MigratePinnedTaskbarShortcutIcon: Boolean;",
            installer,
            StringComparison.Ordinal);
        Assert.Contains(
            "Shortcut.IconLocation := ExpectedIconLocation;",
            installer,
            StringComparison.Ordinal);
        Assert.Contains(
            "SavedIconLocation := Shortcut.IconLocation;",
            installer,
            StringComparison.Ordinal);
        Assert.Contains(
            "if MigratePinnedTaskbarShortcutIcon then",
            installer,
            StringComparison.Ordinal);
        Assert.Contains(
            "DelTree(ExpandConstant('{app}\\CryptoBook-*.ico')",
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
