using CryptoBook.Services;
using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using Xunit;

namespace CryptoBook.Tests;

public sealed class TemporaryDirectoryCleanupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CryptoBook-cleanup-tests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cleanup_PreservesLiveAndProtectedOwnersWithoutAccessDenied(bool export)
    {
        string[] directories = new[] { 0, 4, Environment.ProcessId }.Select(CreateProcessDirectory).ToArray();
        int accessDeniedCount = 0;
        int threadId = Environment.CurrentManagedThreadId;
        void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            if(Environment.CurrentManagedThreadId == threadId && e.Exception is Win32Exception { NativeErrorCode: 5 })
                accessDeniedCount++;
        }

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        try { Cleanup(export); }
        finally { AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException; }

        Assert.Equal(0, accessDeniedCount);
        Assert.All(directories, directory => Assert.True(File.Exists(Path.Combine(directory, "marker.txt"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cleanup_RemovesExitedOwnersAndPreservesUnrecognizedDirectories(bool export)
    {
        string orphan = CreateProcessDirectory(int.MaxValue);
        string unrelated = Path.Combine(root, "unrecognized-name");
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(Path.Combine(unrelated, "marker.txt"), "keep");

        Cleanup(export);

        Assert.False(Directory.Exists(orphan));
        Assert.True(File.Exists(Path.Combine(unrelated, "marker.txt")));
    }

    private string CreateProcessDirectory(int processId)
    {
        string directory = Path.Combine(root, $"{processId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "marker.txt"), "keep");
        return directory;
    }
    private void Cleanup(bool export)
    {
        if(export) DecryptionExportService.CleanupOrphanedDirectories(root);
        else WorkspaceFileOpenService.CleanupOrphanedDirectories(root);
    }
    public void Dispose()
    {
        if(Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
