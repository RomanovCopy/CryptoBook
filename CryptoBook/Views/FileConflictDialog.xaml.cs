using CryptoBook.ViewModels;

using System.Windows;

namespace CryptoBook.Views
{
    public partial class FileConflictDialog: Window
    {
        public FileConflictDialog(FileConflictDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += CloseRequested;
        }

        private void CloseRequested(object? sender, bool? result)
        {
            DialogResult = result;
        }
    }
}
