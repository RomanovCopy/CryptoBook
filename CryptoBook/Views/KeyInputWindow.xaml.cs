using CryptoBook.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CryptoBook.Views
{
    /// <summary>
    /// Логика взаимодействия для KeyInputWindow.xaml
    /// </summary>
    public partial class KeyInputWindow: Window
    {

        private readonly IKeyProvider _keyProvider;
        public KeyInputWindow(IKeyProvider keyProvider)
        {
            InitializeComponent();
            _keyProvider = keyProvider;
            Loaded += (_, _) => PasswordBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            if(!PasswordsAreValid())
                return;

            char[]? password = null;

            try
            {
                password = PasswordBox.SecurePassword.ToCharArray();

                if(password.Length == 0)
                {
                    ErrorText.Text = "Ключ не может быть пустым.";
                    return;
                }

                _keyProvider.SetKey(password);

                DialogResult = true;
                Close();
            } finally
            {
                if(password != null)
                    Array.Clear(password);

                PasswordBox.Clear();
                RepeatPasswordBox.Clear();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
