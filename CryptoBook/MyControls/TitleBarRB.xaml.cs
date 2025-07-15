using CryptoBook.Interfaces;

using Controls = System.Windows.Controls;

namespace CryptoBook.MyControls
{
    /// <summary>
    /// Логика взаимодействия для TitleBarRB.xaml
    /// </summary>
    public partial class TitleBarRB: Controls.UserControl
    {
        public TitleBarRB(ITitleBarRB_ViewModel titleBarRB_ViewModel)
        {
            InitializeComponent();
            DataContext = titleBarRB_ViewModel;
        }
    }
}
