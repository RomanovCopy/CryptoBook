using Autofac;

using CryptoBook.Interfaces;

namespace CryptoBook.MyControls
{
    /// <summary>
    /// Логика взаимодействия для SideMenu.xaml
    /// </summary>
    public partial class SideMenu: System.Windows.Controls.UserControl
    {
        public SideMenu(ILifetimeScope scope)
        {
            DataContext = scope.Resolve<ISideMenuViewModel>();
            InitializeComponent();
        }

    }
}
