using CryptoBook.Infrastructure;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class AtomicFileCommitTests: IDisposable
    {
        private readonly string _directory =
            Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));

        public AtomicFileCommitTests()
        {
            Directory.CreateDirectory(_directory);
        }

        [Fact]
        public void FailedReplace_PreservesTargetAndExistingBackup()
        {
            string target = Path.Combine(_directory, "document.test");
            string backup = target + ".bak";
            string temporary = Path.Combine(_directory, "document.tmp");
            File.WriteAllText(target, "current");
            File.WriteAllText(backup, "previous backup");
            File.WriteAllText(temporary, "new");

            Assert.Throws<IOException>(() =>
                AtomicFileCommit.CommitWithBackup(
                    temporary,
                    target,
                    (_, _, _) => throw new IOException(
                        "Simulated replace failure.")));

            Assert.Equal("current", File.ReadAllText(target));
            Assert.Equal("previous backup", File.ReadAllText(backup));
            Assert.Equal("new", File.ReadAllText(temporary));
            Assert.Equal(
                [target, backup, temporary],
                Directory.EnumerateFiles(_directory).Order());
        }

        [Fact]
        public void SuccessfulReplace_RotatesBackup()
        {
            string target = Path.Combine(_directory, "document.test");
            string backup = target + ".bak";
            string temporary = Path.Combine(_directory, "document.tmp");
            File.WriteAllText(target, "current");
            File.WriteAllText(backup, "previous backup");
            File.WriteAllText(temporary, "new");

            AtomicFileCommit.CommitWithBackup(temporary, target);

            Assert.Equal("new", File.ReadAllText(target));
            Assert.Equal("current", File.ReadAllText(backup));
            Assert.Equal(
                [target, backup],
                Directory.EnumerateFiles(_directory).Order());
        }

        [Fact]
        public void SuccessfulReplaceWithoutBackup_LeavesOnlyTarget()
        {
            string target = Path.Combine(_directory, "document.test");
            string temporary = Path.Combine(_directory, "document.tmp");
            File.WriteAllText(target, "current");
            File.WriteAllText(temporary, "new");

            AtomicFileCommit.CommitWithoutBackup(temporary, target);

            Assert.Equal("new", File.ReadAllText(target));
            Assert.Equal(
                [target],
                Directory.EnumerateFiles(_directory));
        }

        [Fact]
        public void ReplaceReadOnlyTarget_PreservesReadOnlyAttribute()
        {
            string target = Path.Combine(_directory, "readonly.test");
            string temporary = Path.Combine(_directory, "readonly.tmp");
            File.WriteAllText(target, "current");
            File.WriteAllText(temporary, "new");
            File.SetAttributes(
                target,
                File.GetAttributes(target) | FileAttributes.ReadOnly);

            AtomicFileCommit.CommitWithoutBackup(temporary, target);

            Assert.Equal("new", File.ReadAllText(target));
            Assert.True(
                (File.GetAttributes(target) & FileAttributes.ReadOnly) != 0);
        }

        [Fact]
        public void SharedPreviewRead_DoesNotBlockAtomicReplaceOrMove()
        {
            string target = Path.Combine(_directory, "previewed.txt");
            string temporary = Path.Combine(_directory, "replacement.tmp");
            string moved = Path.Combine(_directory, "moved.txt");
            File.WriteAllText(target, "old content");
            File.WriteAllText(temporary, "new content");

            using FileStream preview = SharedFileReadStream.Open(target, 4096);

            AtomicFileCommit.CommitWithoutBackup(temporary, target);
            File.Move(target, moved);

            Assert.Equal("new content", File.ReadAllText(moved));
            using var reader = new StreamReader(preview, leaveOpen: true);
            Assert.Equal("old content", reader.ReadToEnd());
        }

        public void Dispose()
        {
            if(Directory.Exists(_directory))
            {
                foreach(string file in Directory.EnumerateFiles(
                    _directory,
                    "*",
                    SearchOption.AllDirectories))
                {
                    File.SetAttributes(
                        file,
                        File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                }
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
