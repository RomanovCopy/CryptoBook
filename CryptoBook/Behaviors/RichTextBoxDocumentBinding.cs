using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace CryptoBook.Behaviors
{
    public static class RichTextBoxDocumentBinding
    {
        public static readonly DependencyProperty DocumentProperty =
            DependencyProperty.RegisterAttached(
                "Document",
                typeof(FlowDocument),
                typeof(RichTextBoxDocumentBinding),
                new PropertyMetadata(null, OnDocumentChanged));

        public static void SetDocument(
            DependencyObject target,
            FlowDocument? value) =>
            target.SetValue(DocumentProperty, value);

        public static FlowDocument? GetDocument(
            DependencyObject target) =>
            (FlowDocument?)target.GetValue(DocumentProperty);

        private static void OnDocumentChanged(
            DependencyObject target,
            DependencyPropertyChangedEventArgs args)
        {
            if(target is not System.Windows.Controls.RichTextBox richTextBox)
                return;

            FlowDocument replacement = args.NewValue as FlowDocument
                ?? new FlowDocument(new Paragraph());
            if(!ReferenceEquals(richTextBox.Document, replacement))
                richTextBox.Document = replacement;
        }
    }
}
