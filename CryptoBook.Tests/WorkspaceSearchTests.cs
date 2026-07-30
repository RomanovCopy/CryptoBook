using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class WorkspaceSearchTests: IDisposable
{
    private readonly string originalWorkspaceDirectory;
    private readonly string testDirectory;

    public WorkspaceSearchTests()
    {
        originalWorkspaceDirectory =
            Properties.Settings.Default.WorkspaceDirectory;
        testDirectory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.WorkspaceTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
    }

    [Fact]
    public async Task SearchFilesAsync_FindsPartialNameRecursivelyIgnoringCase()
    {
        string nestedDirectory =
            Directory.CreateDirectory(
                Path.Combine(testDirectory, "Вложенная")).FullName;
        string expectedPath =
            Path.Combine(nestedDirectory, "Годовой Отчёт.CBOOK");
        await File.WriteAllTextAsync(expectedPath, "test");
        await File.WriteAllTextAsync(
            Path.Combine(testDirectory, "другая книга.cbook"),
            "test");

        var service = new WorkspaceService();
        service.SetWorkspaceDirectory(testDirectory);

        var outcome = await service.SearchFilesAsync("отчёт");

        var result = Assert.Single(outcome.Results);
        Assert.Equal(expectedPath, result.FullPath);
        Assert.Equal(
            Path.Combine("Вложенная", "Годовой Отчёт.CBOOK"),
            result.RelativePath);
        Assert.False(outcome.IsTruncated);
    }

    [Fact]
    public async Task SearchFilesAsync_StopsAtConfiguredResultLimit()
    {
        for(int index = 0; index < 5; index++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, $"book-{index}.txt"),
                "test");
        }

        var service = new WorkspaceService();
        service.SetWorkspaceDirectory(testDirectory);

        var outcome = await service.SearchFilesAsync(
            "book",
            maxResults: 2);

        Assert.Equal(2, outcome.Results.Count);
        Assert.True(outcome.IsTruncated);
    }

    [Fact]
    public async Task SearchFilesAsync_ObservesCancellation()
    {
        var service = new WorkspaceService();
        service.SetWorkspaceDirectory(testDirectory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SearchFilesAsync(
                "book",
                cancellationToken: cancellation.Token));
    }

    public void Dispose()
    {
        Properties.Settings.Default.WorkspaceDirectory =
            originalWorkspaceDirectory;
        Properties.Settings.Default.Save();

        if(Directory.Exists(testDirectory))
            Directory.Delete(testDirectory, recursive: true);
    }
}
