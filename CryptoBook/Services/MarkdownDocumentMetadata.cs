using CryptoBook.Interfaces;

using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    internal static class MarkdownDocumentMetadata
    {
        private static readonly DependencyProperty SourceProperty =
            DependencyProperty.RegisterAttached(
                "Source",
                typeof(MarkdownTextDocument),
                typeof(MarkdownDocumentMetadata));

        public static void SetSource(
            FlowDocument document,
            MarkdownTextDocument source) =>
            document.SetValue(SourceProperty, source);

        public static MarkdownTextDocument? GetSource(
            FlowDocument document) =>
            document.GetValue(SourceProperty) as MarkdownTextDocument;
    }
}
