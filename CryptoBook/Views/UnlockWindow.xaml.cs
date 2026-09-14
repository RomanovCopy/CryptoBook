using CryptoBook.Interfaces;

using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Windows;
using System.Security;

namespace CryptoBook.Views;

public partial class UnlockWindow : Window
{
    private readonly IKeyResetService keyResetService;

    public UnlockWindow(IKeyResetService keyResetService, string? documentDescription = null)
    {
        this.keyResetService = keyResetService ?? throw new ArgumentNullException(nameof(keyResetService));
        InitializeComponent();
        DocumentDescription.Text = documentDescription ?? string.Empty;
        DocumentDescription.Visibility = string.IsNullOrWhiteSpace(documentDescription)
            ? Visibility.Collapsed : Visibility.Visible;
        Loaded += (_, _) => KeyBox.Focus();
    }

    private async void OnOpen(object sender, RoutedEventArgs args)
    {
        using SecureString password = KeyBox.SecurePassword;
        if(password.Length == 0)
        {
            ErrorText.Text = "Введите ключ.";
            return;
        }

        char[]? characters = null;
        try
        {
            OpenButton.IsEnabled = false;
            ErrorText.Text = string.Empty;
            characters = GC.AllocateArray<char>(password.Length, pinned: true);
            IntPtr value = Marshal.SecureStringToGlobalAllocUnicode(password);
            try { Marshal.Copy(value, characters, 0, characters.Length); }
            finally { Marshal.ZeroFreeGlobalAllocUnicode(value); }

            bool accepted = await keyResetService.TryUnlockAsync(characters);
            if(!accepted)
            {
                ErrorText.Text = "Неверный ключ. Повторите попытку через несколько секунд.";
                return;
            }
            DialogResult = true;
        }
        catch(Exception)
        {
            ErrorText.Text = "Не удалось проверить ключ.";
        }
        finally
        {
            KeyBox.Clear();
            if(characters is not null)
                CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan()));
            OpenButton.IsEnabled = true;
        }
    }
}
