using Microsoft.Win32;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {
        public MainWindow()
        {
            InitializeComponent();
            richTextBox.Document.PagePadding = new Thickness(20); // Отступ по умолчанию
        }

        // Применение жирного начертания
        private void BoldButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTextProperty(TextElement.FontWeightProperty, FontWeights.Bold);
        }

        // Применение курсива
        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTextProperty(TextElement.FontStyleProperty, FontStyles.Italic);
        }

        // Изменение размера шрифта
        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(FontSizeComboBox.SelectedItem is ComboBoxItem selectedItem && double.TryParse(selectedItem.Content.ToString(), out double size))
            {
                ApplyTextProperty(TextElement.FontSizeProperty, size);
            }
        }

        // Изменение цвета текста
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTextProperty(TextElement.ForegroundProperty, Brushes.Red);
        }

        // Применение свойства к выделенному тексту или текущей позиции
        private void ApplyTextProperty(DependencyProperty property, object value)
        {
            TextRange selection = new TextRange(richTextBox.Selection.Start, richTextBox.Selection.End);
            if(selection.IsEmpty)
            {
                richTextBox.Selection.ApplyPropertyValue(property, value);
            } else
            {
                selection.ApplyPropertyValue(property, value);
            }
            richTextBox.Focus();
        }

        // Добавление изображения
        private void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Image files (*.jpg;*.png)|*.jpg;*.png" };
            if(openFileDialog.ShowDialog() == true)
            {
                Image image = new Image
                {
                    Source = new BitmapImage(new Uri(openFileDialog.FileName)),
                    Width = 100,
                    Height = 100
                };
                Paragraph paragraph = new Paragraph();
                paragraph.Inlines.Add(new InlineUIContainer(image));
                richTextBox.Document.Blocks.Add(paragraph);
            }
        }

        // Изменение фона документа
        private void ChangeBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            richTextBox.Document.Background = Brushes.LightYellow;
        }

        // Применение отступов от краев
        private void ApplyMarginButton_Click(object sender, RoutedEventArgs e)
        {
            if(double.TryParse(MarginInput.Text, out double margin))
            {
                richTextBox.Document.PagePadding = new Thickness(margin);
            } else
            {
                MessageBox.Show("Введите корректное значение отступа.");
            }
        }

        // Изменение интервала между строками
        private void LineSpacingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(LineSpacingComboBox.SelectedItem is ComboBoxItem selectedItem && double.TryParse(selectedItem.Content.ToString(), out double lineHeight))
            {
                foreach(Block block in richTextBox.Document.Blocks)
                {
                    if(block is Paragraph paragraph)
                    {
                        paragraph.LineHeight = lineHeight; // Устанавливаем интервал для каждого абзаца
                    }
                }
            }
        }
    }
}