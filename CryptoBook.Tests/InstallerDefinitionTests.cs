using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class InstallerDefinitionTests
{
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
