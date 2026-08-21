using CryptoBook.Models;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class MediaPlayerModelTests
    {
        [Theory]
        [InlineData("photo.jpg", false, true)]
        [InlineData("photo.jpg", true, true)]
        [InlineData("photo.cbook", true, true)]
        [InlineData("photo.cbox", true, true)]
        [InlineData("photo.cbook", false, false)]
        [InlineData("notes.txt", true, false)]
        public void IsImageSequenceCandidate_IncludesSecureContainersOnlyForEncryptedSequence(
            string path,
            bool includeSecureFiles,
            bool expected)
        {
            Assert.Equal(
                expected,
                MediaPlayerModel.IsImageSequenceCandidate(path, includeSecureFiles));
        }

        [Theory]
        [InlineData("clip.mp4", false, true)]
        [InlineData("clip.mkv", false, true)]
        [InlineData("clip.webm", true, true)]
        [InlineData("clip.cbook", true, true)]
        [InlineData("clip.cbox", true, true)]
        [InlineData("clip.cbook", false, false)]
        [InlineData("photo.jpg", true, false)]
        public void IsVideoSequenceCandidate_IncludesSecureContainersOnlyForEncryptedSequence(
            string path,
            bool includeSecureFiles,
            bool expected)
        {
            Assert.Equal(
                expected,
                MediaPlayerModel.IsVideoSequenceCandidate(path, includeSecureFiles));
        }
    }
}
