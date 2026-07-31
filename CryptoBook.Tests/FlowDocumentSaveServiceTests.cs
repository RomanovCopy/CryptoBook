using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.Text;
using System.Windows.Documents;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class FlowDocumentSaveServiceTests
    {
        [WpfFact]
        public async Task ReplacingFile_PreservesPreviousVersionAsBackup()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, "document.test");
            var template = new TestTemplate();
            var handler = new TestHandler(template);
            var service = new FlowDocumentSaveService(
                new WpfDispatcherService(Dispatcher.CurrentDispatcher),
                new DocumentFormatHandlerRegistry([handler]));
            IRichTextBoxService editor = new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService());

            try
            {
                handler.Content = "первая версия";
                await service.SaveToFileAsync(editor, target, template);
                handler.Content = "вторая версия";
                await service.SaveToFileAsync(editor, target, template);

                Assert.Equal(
                    "вторая версия",
                    await File.ReadAllTextAsync(target));
                Assert.Equal(
                    "первая версия",
                    await File.ReadAllTextAsync(target + ".bak"));
                Assert.Empty(
                    Directory.EnumerateFiles(directory, "*.tmp"));
            }
            finally
            {
                if(Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        private sealed class TestTemplate: IFileTemplate
        {
            public string Id => "test";
            public string DisplayName => "Test";
            public string DefaultExtension => ".test";
            public IReadOnlyCollection<string> Extensions => [".test"];
            public string SuggestedBaseName => "document";

            public Task<byte[]> GetInitialContentAsync(
                CancellationToken cancellationToken) =>
                Task.FromResult(Array.Empty<byte>());
        }

        private sealed class TestHandler: IDocumentFormatHandler
        {
            private readonly IFileTemplate template;

            public TestHandler(IFileTemplate template)
            {
                this.template = template;
            }

            public string Content { get; set; } = string.Empty;

            public bool CanHandle(IFileTemplate candidate) =>
                ReferenceEquals(template, candidate);

            public Task LoadAsync(
                FlowDocument document,
                ReadOnlyMemory<byte> content,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<byte[]> SerializeAsync(
                FlowDocument document,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(Encoding.UTF8.GetBytes(Content));
        }

        private sealed class TestParagraphFactory: IParagraphFactory
        {
            public IParagraphService Create(Inline? inline = null)
            {
                var paragraph = new ParagraphService();
                if(inline is not null)
                    paragraph.Inlines.Add(inline);
                return paragraph;
            }
        }
    }
}
