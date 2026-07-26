using CryptoBook.Interfaces;

using System.Windows;

namespace CryptoBook.Views
{
    public partial class DirectoryNameDialog: Window, IWindowWithId, IDialogResult<string?>
    {
        public Guid WindowId { get; } = Guid.NewGuid();

        public static readonly DependencyProperty DirectoryNameProperty =
            DependencyProperty.Register(
                nameof(DirectoryName),
                typeof(string),
                typeof(DirectoryNameDialog),
                new PropertyMetadata(string.Empty));

        public string DirectoryName
        {
            get => (string)GetValue(DirectoryNameProperty);
            set => SetValue(DirectoryNameProperty, value);
        }

        public string? Result { get; private set; }

        public DirectoryNameDialog()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += (_, _) => DirectoryNameTextBox.Focus();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            Result = DirectoryName;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
        }
    }
}
