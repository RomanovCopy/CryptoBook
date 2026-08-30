using CryptoBook.Models;

using System.IO;

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

        [Fact]
        public void BuildSequences_UseFlatCatalogAcrossPhysicalDirectories()
        {
            string root = Path.Combine(Path.GetTempPath(), "CryptoBook.MediaCatalog");
            string selectedImage = Path.Combine(root, "first", "b.jpg");
            string[] catalog =
            [
                Path.Combine(root, "second", "a.png"),
                selectedImage,
                Path.Combine(root, "third", "clip.mp4"),
                Path.Combine(root, "fourth", "notes.txt")
            ];

            IReadOnlyList<string> images = MediaPlayerModel.BuildImageSequence(
                Path.GetFullPath(selectedImage),
                Path.GetDirectoryName(selectedImage),
                includeSecureFiles: false,
                catalog);
            IReadOnlyList<string> videos = MediaPlayerModel.BuildVideoSequence(
                Path.GetFullPath(catalog[2]),
                Path.GetDirectoryName(catalog[2]),
                includeSecureFiles: false,
                catalog);

            Assert.Equal(catalog[..2], images);
            Assert.Equal([catalog[2]], videos);
        }
    }
}
