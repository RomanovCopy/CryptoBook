using System.IO;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace CryptoBook.Services;

/// <summary>Plaintext may exist here only for the duration of an explicit archive operation.</summary>
internal static class ArchiveWorkspace
{
    internal static string CreateTransient(string purpose)
    {
        string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CryptoBook", "ArchiveWork");
        ArchivePathPolicy.EnsureNoLinks(parent);
        Directory.CreateDirectory(parent);
        foreach(string directory in Directory.EnumerateDirectories(parent, ".cryptobook-archive-*"))
        {
            var match = Regex.Match(Path.GetFileName(directory), @"^\.cryptobook-archive-[a-z-]+-(\d+)-[a-f0-9]{32}$");
            if(!match.Success || !int.TryParse(match.Groups[1].Value, out int processId)) continue;
            try { using var process = Process.GetProcessById(processId); if(!process.HasExited) continue; }
            catch(ArgumentException) { }
            ArchivePathPolicy.EnsureNoLinks(directory);
            try { Directory.Delete(directory, true); }
            catch(Exception exception) when(exception is IOException or UnauthorizedAccessException)
            { throw new IOException(Infrastructure.LocalizationManager.Format("Archive.CleanupFailed", directory), exception); }
        }
        return Create(parent, purpose);
    }

    internal static string Create(string parent, string purpose)
    {
        ArchivePathPolicy.EnsureNoLinks(parent);
        string path = Path.Combine(parent, $".cryptobook-{purpose}-{Environment.ProcessId}-{Guid.NewGuid():N}");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User!;
        security.SetOwner(user);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).Create(security);
        return path;
    }
}
