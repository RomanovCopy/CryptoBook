using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Autofac;

using CryptoBook.Interfaces;
using CryptoBook.ViewModels;

namespace CryptoBook.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow ( ILifetimeScope scope )
        {
            DataContext = scope.Resolve<IMainWindowViewModel>();

            Thread.CurrentThread.CurrentCulture = new CultureInfo(Properties.Settings.Default.CultureInfo);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(Properties.Settings.Default.CultureInfo);


            InitializeComponent( );

            Closed += (s, e) => scope.Dispose();
        }
    }
}