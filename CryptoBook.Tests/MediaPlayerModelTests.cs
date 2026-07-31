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
    }
}
