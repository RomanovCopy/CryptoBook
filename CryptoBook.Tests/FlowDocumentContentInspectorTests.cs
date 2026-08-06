using CryptoBook.Services;

using System.Windows.Controls;
using System.Windows.Documents;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class FlowDocumentContentInspectorTests
    {
        private readonly FlowDocumentContentInspector inspector = new();

        [WpfFact]
        public void EmptyOrInvisibleText_IsNotPrintable()
        {
            var document = new FlowDocument(
                new Paragraph(new Run(" \r\n\t\u200B")));

            Assert.False(inspector.HasPrintableContent(document));
        }

        [WpfFact]
        public void VisibleText_IsPrintable()
        {
            var document = new FlowDocument(
                new Paragraph(new Run("CryptoBook")));

            Assert.True(inspector.HasPrintableContent(document));
        }

        [WpfFact]
        public void EmbeddedElement_IsPrintable()
        {
            var document = new FlowDocument(
                new Paragraph(new InlineUIContainer(new Image())));

            Assert.True(inspector.HasPrintableContent(document));
        }

        [WpfFact]
        public void TableStructure_IsPrintable()
        {
            var table = new Table();
            var rowGroup = new TableRowGroup();
            var row = new TableRow();
            row.Cells.Add(new TableCell(new Paragraph()));
            rowGroup.Rows.Add(row);
            table.RowGroups.Add(rowGroup);
            var document = new FlowDocument(table);

            Assert.True(inspector.HasPrintableContent(document));
        }
    }
}
