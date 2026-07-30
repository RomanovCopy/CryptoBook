using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class FilePreviewViewModelTests
    {
        [Fact]
        public async Task SelectAsync_ExposesTextPreviewAndMetadata()
        {
            var service = new StubPreviewService(
                new FilePreviewContent(FilePreviewKind.Text, Text: "preview"));
            var viewModel = new FilePreviewViewModel(service);
            var file = new FileItem
            {
                Name = "notes.txt",
                FullPath = @"C:\notes.txt",
                Extension = ".txt",
                Size = 2048,
                LastWriteTimeUtc = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc)
            };

            await viewModel.SelectAsync(file);

            Assert.Equal(FilePreviewKind.Text, viewModel.PreviewKind);
            Assert.Equal("preview", viewModel.Text);
            Assert.Equal("notes.txt", viewModel.FileName);
            Assert.Contains("КБ", viewModel.FileDetails);
        }

        [Fact]
        public async Task SelectAsync_ClearsPreviewForDirectory()
        {
            var viewModel = new FilePreviewViewModel(
                new StubPreviewService(
                    new FilePreviewContent(FilePreviewKind.Text, Text: "preview")));

            await viewModel.SelectAsync(null);

            Assert.Equal(FilePreviewKind.Empty, viewModel.PreviewKind);
            Assert.Contains("Выберите файл", viewModel.Message);
        }

        private sealed class StubPreviewService: IFilePreviewService
        {
            private readonly FilePreviewContent _content;

            public StubPreviewService(FilePreviewContent content)
            {
                _content = content;
            }

            public Task<FilePreviewContent> LoadAsync(
                IFileItem file,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_content);
            }
        }
    }
}
