using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileTemplateTests
{
    [Fact]
    public void PdfTemplate_IsExternalAndCannotBeCreated()
    {
        IFileTemplate template = new PdfFileTemplate();

        Assert.True(template.CanHandleExtension(".pdf"));
        Assert.True(template.CanHandleExtension(".PDF"));
        Assert.Equal(FileOpenMode.External, template.OpenMode);
        Assert.False(template.CanCreate);
    }

    [Fact]
    public void MediaTemplates_UseMediaOpenMode()
    {
        Assert.Equal(FileOpenMode.Media, new ImageFileTemplate().OpenMode);
        Assert.Equal(FileOpenMode.Media, new VideoFileTemplate().OpenMode);
    }

    [Fact]
    public async Task ImageTemplate_CreatesValidPngHeader()
    {
        byte[] content = await new ImageFileTemplate()
            .GetInitialContentAsync(CancellationToken.None);

        Assert.True(content.Length > 8);
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            content[..8]);
    }

    [Fact]
    public void Registry_FindsPdfTemplate()
    {
        var pdf = new PdfFileTemplate();
        var registry = new FileTemplateRegistry([pdf]);

        Assert.Same(pdf, registry.GetById("pdf"));
        Assert.Contains(
            registry.GetAll(),
            template => template.CanHandleExtension(".PDF"));
    }

    [Fact]
    public async Task XamlPackageTemplate_CreatesNonEmptyContent()
    {
        byte[] content = await new XamlPackageFileTemplate()
            .GetInitialContentAsync(CancellationToken.None);

        Assert.NotEmpty(content);
    }
}
