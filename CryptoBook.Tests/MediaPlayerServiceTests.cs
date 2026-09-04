using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class MediaPlayerServiceTests
    {
        [Theory]
        [InlineData(-1, 10, 0)]
        [InlineData(0, 10, 0)]
        [InlineData(4, 10, 4)]
        [InlineData(10, 10, 9.75)]
        [InlineData(12, 10, 9.75)]
        public void ClampSeekPosition_KeepsRequestsInsidePlayableRange(
            double requestedSeconds,
            double durationSeconds,
            double expectedSeconds)
        {
            TimeSpan actual = MediaPlayerService.ClampSeekPosition(
                TimeSpan.FromSeconds(requestedSeconds),
                TimeSpan.FromSeconds(durationSeconds));

            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), actual);
        }

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
