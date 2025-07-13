using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Controls = System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CryptoBook.Interfaces;

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
