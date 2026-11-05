using CryptoBook.Interfaces;

using System.ComponentModel;
using System.Windows;

namespace CryptoBook.Views
{
    /// <summary>
    /// Логика взаимодействия для ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow: Window
    {
        private bool operationPending;

        public ProgressWindow(IProgressViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        internal void BeginOperation() => operationPending = true;

        internal void CompleteOperation() => operationPending = false;

        protected override void OnClosing(CancelEventArgs e)
        {
            if(operationPending)
            {
                e.Cancel = true;
                if(DataContext is IProgressViewModel viewModel &&
                   viewModel.Canceled.CanExecute(null))
                {
                    viewModel.Canceled.Execute(null);
                }
            }
            base.OnClosing(e);
        }

    }
}
