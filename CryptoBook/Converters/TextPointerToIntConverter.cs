using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Documents;

using Controls = System.Windows.Controls;

namespace CryptoBook.Converters
{
    internal class TextPointerToIntConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return GetCaretPosition((Controls.RichTextBox)parameter);
            } catch(Exception ex) { ErrorWindow(ex); return -1; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {

            return GetCaretPosition((Controls.RichTextBox)parameter);
        }

        private int GetCaretPosition(Controls.RichTextBox textBox)
        {
            try
            {
                int length = -1;
                var start = textBox.Document.ContentStart;
                var here = textBox.CaretPosition;
                var range = new TextRange(start, here);
                length = range.Text.Length;
                return length;
            } catch(Exception e) { ErrorWindow(e); return -1; }
        }

        private TextPointer GetTextPointer(Controls.RichTextBox textBox, int length)
        {
            try
            {
                TextPointer pointer = textBox.Document.ContentStart;
                TextPointer temp = textBox.Document.ContentStart;
                for(int i = 0; i < length; i++)
                {
                    pointer.GetNextContextPosition(LogicalDirection.Forward);
                }
                return pointer;
            } catch(Exception e) { ErrorWindow(e); return null; }
        }

        private void ErrorWindow(Exception e, [CallerMemberName] string name = "")
        {
            var mytype = GetType().ToString().Split('.').LastOrDefault();
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            { System.Windows.MessageBox.Show(e.Message, $"{mytype}.{name}"); }));
        }

    }
}
