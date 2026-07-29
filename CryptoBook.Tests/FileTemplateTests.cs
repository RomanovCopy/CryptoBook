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
    public void Registry_FindsPdfTemplate()
    {
        var pdf = new PdfFileTemplate();
        var registry = new FileTemplateRegistry([pdf]);

        Assert.Same(pdf, registry.GetById("pdf"));
        Assert.Contains(
            registry.GetAll(),
            template => template.CanHandleExtension(".PDF"));
    }
}
