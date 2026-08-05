using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.ComponentModel;
using System.Windows.Documents;
using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class DocumentTitleProviderTests
    {
        [Fact]
        public void FileDisplayName_PreservesExistingExtension()
        {
            var service = new FileDisplayNameService();

            string result = service.GetDisplayName(
                Path.Combine("C:\\", "Documents", "notes.rtf"),
                ".txt");

            Assert.Equal("notes.rtf", result);
        }

        [Fact]
        public void FileDisplayName_AddsDefaultExtensionWhenMissing()
        {
            var service = new FileDisplayNameService();

            string result = service.GetDisplayName("New document", "rtf");

            Assert.Equal("New document.rtf", result);
        }

        [Fact]
        public void FileDisplayName_ReturnsEmptyTitleWithoutFile()
        {
            var service = new FileDisplayNameService();

            Assert.Equal(string.Empty, service.GetDisplayName(null, ".txt"));
            Assert.Equal(string.Empty, service.GetDisplayName(" ", ".txt"));
        }

        [Fact]
        public void DocumentTitle_TracksSessionPathWithExtension()
        {
            var session = new TestDocumentSession();
            using var provider = new DocumentTitleProvider(
                session,
                new FileDisplayNameService());
            var changedProperties = new List<string?>();
            provider.PropertyChanged += (_, args) =>
                changedProperties.Add(args.PropertyName);

            session.SetFilePath(
                Path.Combine("C:\\", "Documents", "report.XamlPackage"));

            Assert.Equal("report.XamlPackage", provider.Title);
            Assert.Equal(
                Path.Combine("C:\\", "Documents", "report.XamlPackage"),
                provider.Path);
            Assert.Contains(nameof(IDocumentTitleProvider.Title), changedProperties);
            Assert.Contains(nameof(IDocumentTitleProvider.Path), changedProperties);
        }

        [Fact]
        public void DocumentTitle_IsEmptyWithoutOpenFile()
        {
            using var provider = new DocumentTitleProvider(
                new TestDocumentSession(),
                new FileDisplayNameService());

            Assert.Equal(string.Empty, provider.Title);
            Assert.Null(provider.Path);
        }

        [Fact]
        public void Dispose_UnsubscribesFromDocumentSession()
        {
            var session = new TestDocumentSession();
            var provider = new DocumentTitleProvider(
                session,
                new FileDisplayNameService());
            int notifications = 0;
            provider.PropertyChanged += (_, _) => notifications++;
            provider.Dispose();

            session.SetFilePath(
                Path.Combine("C:\\", "Documents", "after-dispose.txt"));

            Assert.Equal(0, notifications);
        }

        private sealed class TestDocumentSession: IDocumentSession
        {
            public string? FilePath { get; private set; }
            public string DisplayName { get; private set; } = string.Empty;
            public IFileTemplate? Template { get; private set; }
            public bool IsDirty { get; private set; }
            public long Revision { get; private set; }
            public long SavedRevision { get; private set; }
            public bool HasDocument => IsDirty ||
                !string.IsNullOrWhiteSpace(FilePath) ||
                !string.IsNullOrWhiteSpace(DisplayName);

            public event PropertyChangedEventHandler? PropertyChanged;

            public void SetFilePath(string? filePath)
            {
                FilePath = filePath;
                DisplayName = filePath is null
                    ? string.Empty
                    : Path.GetFileName(filePath);
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(FilePath)));
            }

            public void Open(string filePath, IFileTemplate template)
            {
                FilePath = filePath;
                Template = template;
                IsDirty = false;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(FilePath)));
            }

            public void Open(
                string filePath,
                IFileTemplate template,
                FlowDocument document) => Open(filePath, template);

            public void Close() => SetFilePath(null);

            public void MarkDirty()
            {
                Revision++;
                IsDirty = true;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(IsDirty)));
            }

            public void MarkSaved(string filePath, IFileTemplate template) =>
                Open(filePath, template);

            public void MarkSaved(
                string filePath,
                IFileTemplate template,
                long savedRevision)
            {
                SavedRevision = savedRevision;
                Open(filePath, template);
            }

            public void Rename(string filePath) => SetFilePath(filePath);

            public void SetDisplayName(string displayName)
            {
                DisplayName = displayName;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }
    }
}
