using Autofac;

using Controls = System.Windows.Controls;

namespace CryptoBook.MyControls
{
    /// <summary>
    /// Логика взаимодействия для MyFrame.xaml
    /// </summary>
    public partial class MyFrame: Controls.UserControl
    {
        public MyFrame(ILifetimeScope scope)
        {
            InitializeComponent();
        }
    }
}
