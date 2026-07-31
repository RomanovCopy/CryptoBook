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

        public void Dispose()
        {
            if(Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }
}
