using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.Windows.Documents;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class DocumentSessionTests
    {
        [WpfFact]
        public void DocumentChange_MarksSessionDirty_AndSaveResetsIt()
        {
            IRichTextBoxService richTextBox = new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService());
            var session = new DocumentSession(richTextBox);
            var template = new XamlPackageFileTemplate();
            string path = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook-session-test.XamlPackage");
            session.Open(path, template);

            Assert.False(session.IsDirty);

            richTextBox.Selection.Text = "изменение";

            Assert.True(session.IsDirty);

            session.MarkSaved(path, template);

            Assert.False(session.IsDirty);
            Assert.Equal(Path.GetFullPath(path), session.FilePath);
            Assert.Same(template, session.Template);
        }

        [WpfFact]
        public void ImageEditor_MarksSessionDirty()
        {
            IRichTextBoxService richTextBox = new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService());
            var session = new DocumentSession(richTextBox);
            var template = new XamlPackageFileTemplate();
            session.Open(
                Path.Combine(
                    Path.GetTempPath(),
                    "CryptoBook-image-test.XamlPackage"),
                template);
            var image = new System.Windows.Controls.Image
            {
                Source = System.Windows.Media.Imaging.BitmapSource.Create(
                    2,
                    1,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null,
                    new byte[8],
                    8)
            };
            var editor = new EmbeddedImageEditor(session);

            editor.ResizeToWidth(image, 100, 500);

            Assert.True(session.IsDirty);
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
