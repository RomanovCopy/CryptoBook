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
        public void SavingOlderRevision_DoesNotClearNewerChanges()
        {
            IRichTextBoxService richTextBox = new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService());
            var session = new DocumentSession(richTextBox);
            var template = new XamlPackageFileTemplate();
            string path = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook-revision-test.XamlPackage");
            session.Open(path, template);

            richTextBox.Selection.Text = "первая версия";
            long revisionBeingSaved = session.Revision;
            richTextBox.Selection.Text = "новая версия";

            session.MarkSaved(path, template, revisionBeingSaved);

            Assert.True(session.IsDirty);
            Assert.Equal(revisionBeingSaved, session.SavedRevision);
            Assert.True(session.Revision > session.SavedRevision);
        }

        [WpfFact]
        public void Rename_UpdatesPath_AndPreservesDirtyState()
        {
            IRichTextBoxService richTextBox = new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService());
            var session = new DocumentSession(richTextBox);
            var template = new XamlPackageFileTemplate();
            string originalPath = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook-original.XamlPackage");
            string renamedPath = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook-renamed.XamlPackage");
            session.Open(originalPath, template);
            session.MarkDirty();

            session.Rename(renamedPath);

            Assert.Equal(Path.GetFullPath(renamedPath), session.FilePath);
            Assert.True(session.IsDirty);
            Assert.Same(template, session.Template);
        }

        [WpfFact]
        public void SetDisplayName_NamesUnsavedDocument()
        {
            IRichTextBoxService richTextBox = new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService());
            var session = new DocumentSession(richTextBox);

            session.SetDisplayName("Моя книга.XamlPackage");

            Assert.Null(session.FilePath);
            Assert.Equal("Моя книга.XamlPackage", session.DisplayName);
        }

        [WpfFact]
        public void Close_ClearsEditorAndDocumentIdentity()
        {
            IRichTextBoxService richTextBox = new RichTextBoxService(
                new TestParagraphFactory(),
                new TestUriNavigationService());
            var session = new DocumentSession(richTextBox);
            var template = new XamlPackageFileTemplate();
            session.Open(
                Path.Combine(Path.GetTempPath(), "CryptoBook-close-test.XamlPackage"),
                template);
            richTextBox.Selection.Text = "content";

            session.Close();

            Assert.False(session.HasDocument);
            Assert.False(session.IsDirty);
            Assert.Null(session.FilePath);
            Assert.Equal(string.Empty, session.DisplayName);
            Assert.Null(session.Template);
            Assert.Equal(
                string.Empty,
                new TextRange(
                    richTextBox.Document.ContentStart,
                    richTextBox.Document.ContentEnd).Text.Trim());
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

        [WpfFact]
        public void ImageEditor_KeepsFloatingContainerWidthInSync()
        {
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
            var figure = new Figure(new BlockUIContainer(image));
            _ = new FlowDocument(new Paragraph(figure));
            var editor = new EmbeddedImageEditor();

            editor.ResizeToWidth(image, 180, 500);

            Assert.Equal(180, image.Width);
            Assert.Equal(180, figure.Width.Value);
            Assert.Equal(
                System.Windows.FigureUnitType.Pixel,
                figure.Width.FigureUnitType);
        }

        [WpfFact]
        public void ImageEditor_FitsImageWithinPageBounds()
        {
            var image = new System.Windows.Controls.Image
            {
                Source = System.Windows.Media.Imaging.BitmapSource.Create(
                    100,
                    300,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.BlackWhite,
                    null,
                    new byte[3900],
                    13)
            };
            var editor = new EmbeddedImageEditor();

            editor.FitWithin(
                image,
                maximumWidth: 200,
                maximumHeight: 100);

            Assert.Equal(100, image.Height, precision: 6);
            Assert.Equal(100d / 3, image.Width, precision: 6);
        }

        [WpfFact]
        public void ImageEditor_DoesNotLimitImageForInfiniteDocument()
        {
            var image = new System.Windows.Controls.Image
            {
                Source = System.Windows.Media.Imaging.BitmapSource.Create(
                    100,
                    300,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.BlackWhite,
                    null,
                    new byte[3900],
                    13)
            };
            var editor = new EmbeddedImageEditor();

            editor.FitWithin(
                image,
                double.PositiveInfinity,
                double.PositiveInfinity);

            Assert.Equal(100, image.Width);
            Assert.Equal(300, image.Height);
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
