using CryptoBook.Services;

using Drawing = System.Drawing;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentBackgroundPreferenceTests
{
    [Fact]
    public void Store_RestoresArgbColorAcrossInstances()
    {
        string original =
            Properties.Settings.Default.DocumentBackgroundColor;

        try
        {
            var first = new UserDocumentBackgroundPreferenceStore();
            Drawing.Color selected =
                Drawing.Color.FromArgb(128, 12, 34, 56);

            first.Save(selected);

            var second = new UserDocumentBackgroundPreferenceStore();
            Drawing.Color restored =
                Assert.IsType<Drawing.Color>(second.Load());

            Assert.Equal(selected.ToArgb(), restored.ToArgb());
            Assert.Equal(
                "#800C2238",
                Properties.Settings.Default.DocumentBackgroundColor);
        }
        finally
        {
            Properties.Settings.Default.DocumentBackgroundColor = original;
            Properties.Settings.Default.Save();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-color")]
    [InlineData("#123456")]
    public void Store_InvalidOrMissingValue_UsesThemeDefault(string stored)
    {
        string original =
            Properties.Settings.Default.DocumentBackgroundColor;

        try
        {
            Properties.Settings.Default.DocumentBackgroundColor = stored;

            Assert.Null(
                new UserDocumentBackgroundPreferenceStore().Load());
        }
        finally
        {
            Properties.Settings.Default.DocumentBackgroundColor = original;
        }
    }
}
