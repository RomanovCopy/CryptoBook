using CryptoBook.Services;

using System.ComponentModel;
using System.Windows;

namespace CryptoBook.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {
        private readonly DocumentCloseCoordinator closeCoordinator;

        public MainWindow(DocumentCloseCoordinator closeCoordinator)
        {
            this.closeCoordinator = closeCoordinator
                ?? throw new ArgumentNullException(nameof(closeCoordinator));

            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private async void OnLoaded(object sender, RoutedEventArgs args)
        {
            Loaded -= OnLoaded;
            await closeCoordinator.InitializeAsync();
        }

        private async void OnClosing(object? sender, CancelEventArgs args)
        {
            if(closeCoordinator.IsCloseApproved)
                return;

            args.Cancel = true;
            if(await closeCoordinator.TryApproveCloseAsync())
            {
                _ = Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(Close));
            }
        }
    }
}
