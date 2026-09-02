using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class MediaPlayerServiceTests
    {
        [Fact]
        public void PackagedFfmpegRuntime_IsAvailable()
        {
            string runtimePath = MediaPlayerService.ResolveFFmpegPath();

            Assert.All(
                MediaPlayerService.RequiredFfmpegLibraries,
                library => Assert.True(
                    File.Exists(Path.Combine(runtimePath, library)),
                    $"Packaged FFmpeg library is missing: {library}"));
        }
    }
}
