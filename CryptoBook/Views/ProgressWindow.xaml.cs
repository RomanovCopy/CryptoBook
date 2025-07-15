using CryptoBook.Interfaces;

using System.Windows;

namespace CryptoBook.Views
{
    /// <summary>
    /// Логика взаимодействия для ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow: Window
    {

        public ProgressWindow(IProgressViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

    }
}
