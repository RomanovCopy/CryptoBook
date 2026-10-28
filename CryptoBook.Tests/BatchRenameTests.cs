using CryptoBook.DTO;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class BatchRenameTests
{
    [Theory]
    [InlineData("[N]", "[E]", "photo.JPG")]
    [InlineData("Photo_[C]", "[E]", "Photo_007.JPG")]
    [InlineData("[N2-4]_[N4-]", "[E1-2]", "hot_to.JP")]
    [InlineData("[N9-]ok", "", "ok")]
    [InlineData("[N1]", "[E]", "p.JPG")]
    public void MasksExpandWithCounterAndRanges(string mask, string extension, string expected)
    {
        Assert.Equal(expected, BatchRenamePattern.BuildName("photo.JPG", 2,
            new() { NameMask = mask, ExtensionMask = extension, CounterStart = 3, CounterStep = 2 }));
    }

    [Fact]
    public void CurrentDateAndTimeMasksUseDayMonthYearAnd24HourTime()
    {
        Assert.Equal("28102026_153045_photo.JPG", BatchRenamePattern.BuildName("photo.JPG", 0,
            new() { NameMask = "[DMY]_[HMS]_[N]" }, new DateTime(2026, 10, 28, 15, 30, 45)));
        Assert.Equal("02012026_000503.02012026_000503", BatchRenamePattern.BuildName("a.txt", 0,
            new() { NameMask = "[DMY]_[HMS]", ExtensionMask = "[DMY]_[HMS]" },
            new DateTime(2026, 1, 2, 0, 5, 3)));
    }

    [Fact]
    public async Task CurrentTimeIsSharedByEveryFileInPreviewAndDoesNotUseFileTimestamp()
    {
        using var directory = new TestDirectory();
        string a = directory.File("a.txt", "A"), b = directory.File("b.txt", "B");
        System.IO.File.SetLastWriteTime(a, new DateTime(2000, 1, 1));
        System.IO.File.SetLastWriteTime(b, new DateTime(2001, 2, 2));
        var entries = await new BatchRenameService().PreviewAsync([a, b],
            new() { NameMask = "[DMY]_[HMS]_[C]" });
        Assert.Equal(entries[0].NewName[..15], entries[1].NewName[..15]);
        Assert.DoesNotContain("01012000", entries[0].NewName);
        Assert.DoesNotContain("02022001", entries[1].NewName);
        Assert.All(entries, e => Assert.Null(e.Error));
    }

    [Fact]
    public void MasksPreserveUnicodeTextElements()
    {
        Assert.Equal("😀é.txt", BatchRenamePattern.BuildName("a😀éz.txt", 0,
            new() { NameMask = "[N2-3]" }));
    }

    [Fact]
    public void ReplacementThenCaseAppliesToFullName()
    {
        Assert.Equal("image_01.png", BatchRenamePattern.BuildName("PHOTO_25.JPG", 0,
            new() { Search = @"PHOTO_\d+\.JPG", Replacement = "Image_01.PNG", UseRegex = true,
                Case = RenameCase.Lower }));
        Assert.Equal("ab.txt", BatchRenamePattern.BuildName("AB.txt", 0,
            new() { Search = "AB", Replacement = "ab", MatchCase = true }));
    }

    [Theory]
    [InlineData("[N0]")]
    [InlineData("[N4-2]")]
    [InlineData("[X]")]
    [InlineData("[N")]
    public void InvalidMaskIsRejected(string mask) => Assert.Throws<ArgumentException>(() =>
        BatchRenamePattern.BuildName("photo.txt", 0, new() { NameMask = mask }));

    [Theory]
    [InlineData("CON.txt")]
    [InlineData("lpt1")]
    [InlineData("COM¹.log")]
    [InlineData("a.")]
    [InlineData("a ")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("..")]
    [InlineData("")]
    public void InvalidWindowsNamesAreRejected(string name) => Assert.NotNull(BatchRenamePattern.ValidateName(name));

    [Fact]
    public async Task PreviewDetectsDuplicatesExistingFileAndDirectory()
    {
        using var directory = new TestDirectory();
        string a = directory.File("a.txt", "A"), b = directory.File("b.txt", "B");
        var service = new BatchRenameService();
        Assert.All(await service.PreviewAsync([a, b], new() { NameMask = "same" }),
            e => Assert.NotNull(e.Error));
        Assert.NotNull(Assert.Single(await service.PreviewAsync([a], new() { NameMask = "b" })).Error);
        Directory.CreateDirectory(Path.Combine(directory.Path, "folder.txt"));
        Assert.NotNull(Assert.Single(await service.PreviewAsync([a], new() { NameMask = "folder" })).Error);
        Assert.Equal("A", System.IO.File.ReadAllText(a));
    }

    [Fact]
    public async Task SwapAndUndoPreserveContents()
    {
        using var directory = new TestDirectory();
        string a = directory.File("a.txt", "A"), b = directory.File("b.txt", "B");
        var service = new BatchRenameService();
        var result = await service.RenameAsync([new(a, "b.txt"), new(b, "a.txt")]);
        Assert.True(result.Success, result.Error);
        Assert.Equal("B", System.IO.File.ReadAllText(a));
        Assert.Equal("A", System.IO.File.ReadAllText(b));
        var undo = await service.RenameAsync(result.ChangedPaths.Select(p =>
            new BatchRenameEntry(p.Value, Path.GetFileName(p.Key))).ToArray());
        Assert.True(undo.Success, undo.Error);
        Assert.Equal("A", System.IO.File.ReadAllText(a));
        Assert.Equal("B", System.IO.File.ReadAllText(b));
        Assert.Equal(2, Directory.GetFiles(directory.Path).Length);
    }

    [Fact]
    public async Task CaseOnlyRenameAndExtensionRemovalWork()
    {
        using var directory = new TestDirectory();
        string source = directory.File("a.TXT", "content");
        var service = new BatchRenameService();
        Assert.True((await service.RenameAsync([new(source, "A.txt")])).Success);
        string renamed = Assert.Single(Directory.GetFiles(directory.Path));
        Assert.Equal("A.txt", Path.GetFileName(renamed));
        Assert.True((await service.RenameAsync([new(renamed, "A")])).Success);
        Assert.Equal("content", System.IO.File.ReadAllText(Path.Combine(directory.Path, "A")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task CancellationDuringStagingOrFinalMovesRollsBack(int cancelAfter)
    {
        using var directory = new TestDirectory();
        string a = directory.File("a.txt", "A"), b = directory.File("b.txt", "B");
        using var cancellation = new CancellationTokenSource();
        int steps = 0;
        var result = await new BatchRenameService().RenameAsync([new(a, "b.txt"), new(b, "a.txt")],
            new CallbackProgress(_ => { if(++steps == cancelAfter) cancellation.Cancel(); }), cancellation.Token);
        Assert.False(result.Success);
        Assert.True(result.Cancelled);
        Assert.Empty(result.ChangedPaths);
        Assert.Equal("A", System.IO.File.ReadAllText(a));
        Assert.Equal("B", System.IO.File.ReadAllText(b));
        Assert.Equal(2, Directory.GetFiles(directory.Path).Length);
    }

    [Fact]
    public async Task LockedSourceRollsBackEarlierFiles()
    {
        using var directory = new TestDirectory();
        string a = directory.File("a.txt", "A"), b = directory.File("b.txt", "B");
        using(var locked = System.IO.File.Open(b, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await new BatchRenameService().RenameAsync([new(a, "x.txt"), new(b, "y.txt")]);
            Assert.False(result.Success);
            Assert.Empty(result.ChangedPaths);
        }
        Assert.Equal("A", System.IO.File.ReadAllText(a));
        Assert.Equal("B", System.IO.File.ReadAllText(b));
    }

    [Fact]
    public async Task DestinationCreatedDuringRenameIsNeverOverwritten()
    {
        using var directory = new TestDirectory();
        string a = directory.File("a.txt", "A");
        var result = await new BatchRenameService().RenameAsync([new(a, "target.txt")],
            new CallbackProgress(_ => directory.File("target.txt", "external")));
        Assert.False(result.Success);
        Assert.Empty(result.ChangedPaths);
        Assert.Equal("A", System.IO.File.ReadAllText(a));
        Assert.Equal("external", System.IO.File.ReadAllText(Path.Combine(directory.Path, "target.txt")));
    }

    [Fact]
    public async Task RecoveryFailureReportsPreservedLocationAndDoesNotOverwriteNewSource()
    {
        using var directory = new TestDirectory();
        string a = directory.File("a.txt", "original");
        using var cancellation = new CancellationTokenSource();
        var result = await new BatchRenameService().RenameAsync([new(a, "target.txt")],
            new CallbackProgress(_ => { directory.File("a.txt", "external"); cancellation.Cancel(); }), cancellation.Token);
        Assert.False(result.Success);
        string actual = Assert.Single(result.ChangedPaths).Value;
        Assert.Equal("original", System.IO.File.ReadAllText(actual));
        Assert.Equal("external", System.IO.File.ReadAllText(a));
        Assert.Contains(actual, result.Error!);
    }

    [Fact]
    public async Task StalePreviewAndInvalidRegexCannotRenameFiles()
    {
        using var directory = new TestDirectory();
        string a = directory.File("a.txt", "A");
        var service = new BatchRenameService();
        var plan = await service.PreviewAsync([a], new() { NameMask = "target" });
        directory.File("target.txt", "external");
        Assert.False((await service.RenameAsync(plan)).Success);
        Assert.NotNull(Assert.Single(await service.PreviewAsync([a], new() { Search = "[", UseRegex = true })).Error);
        Assert.Equal("A", System.IO.File.ReadAllText(a));
    }

    private sealed class CallbackProgress(Action<int> callback): IProgress<int>
    {
        public void Report(int value) => callback(value);
    }

    internal sealed class TestDirectory: IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(
            AppContext.BaseDirectory, "batch-rename-tests", Guid.NewGuid().ToString("N"))).FullName;
        public string File(string name, string text)
        {
            string path = System.IO.Path.Combine(Path, name);
            System.IO.File.WriteAllText(path, text);
            return path;
        }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
