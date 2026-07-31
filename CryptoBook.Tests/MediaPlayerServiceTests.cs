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

            Assert.True(File.Exists(Path.Combine(runtimePath, "avcodec-61.dll")));
            Assert.True(File.Exists(Path.Combine(runtimePath, "avutil-59.dll")));
        }
    }
}
