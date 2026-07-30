using CryptoBook.ViewModels;

using System.Windows;

namespace CryptoBook.Views
{
    public partial class TextInputDialog: Window
    {
        public TextInputDialog(TextInputDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += (_, result) => DialogResult = result;
            Loaded += (_, _) =>
            {
                ValueTextBox.Focus();
                ValueTextBox.SelectAll();
            };
        }
    }
}
