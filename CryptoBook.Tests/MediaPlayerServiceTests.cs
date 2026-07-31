using CryptoBook.Services;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class MediaPlayerServiceTests
    {
        [StaFact]
        public void Constructor_LoadsPackagedFfmpegRuntime()
        {
            using var service = new MediaPlayerService();

            Assert.NotNull(service.PlayerInstance);
        }
    }
}
