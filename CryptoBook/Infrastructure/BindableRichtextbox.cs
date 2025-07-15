using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml;
namespace CryptoBook.Infrastructure
{
    public class BindableRichtextbox: System.Windows.Controls.RichTextBox
    {
        public static readonly DependencyProperty DocumentContentProperty =
            DependencyProperty.Register(
                nameof(DocumentContent),
                typeof(FlowDocument),
                typeof(BindableRichtextbox),
                new PropertyMetadata(null, OnDocumentContentChanged));

        public FlowDocument DocumentContent
        {
            get => (FlowDocument)GetValue(DocumentContentProperty);
            set => SetValue(DocumentContentProperty, value);
        }

        private static void OnDocumentContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var richTextBox = (BindableRichtextbox)d;
            richTextBox.Document = e.NewValue as FlowDocument ?? new FlowDocument();
        }


    }
}
