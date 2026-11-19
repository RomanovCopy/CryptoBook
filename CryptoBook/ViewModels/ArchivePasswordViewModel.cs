using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Security;
using System.Windows.Input;

namespace CryptoBook.ViewModels;

public sealed class ArchivePasswordViewModel: ViewModelBase, IWindowWithId, IDialogResult<SecureString>
{
    private SecureString? password;
    public ArchivePasswordViewModel(IWindowContext context, IWindowManager windows)
    {
        Source = context.Get<string>("source");
        Hint = ArchivePathPolicy.Text(context.Get<bool>("retry") ? "PasswordRetry" : "PasswordRequired");
        AcceptCommand = new RelayCommand(_ =>
        {
            Result = Password?.Copy();
            Password = null;
            windows.CloseWindow(WindowId);
        }, _ => Password?.Length > 0);
        CancelCommand = new RelayCommand(_ => windows.CloseWindow(WindowId));
        Closed = new RelayCommand(_ => Password = null);
    }
    public Guid WindowId { get; } = Guid.NewGuid();
    public string Source { get; }
    public string Hint { get; }
    public SecureString? Result { get; private set; }
    public SecureString? Password
    {
        get => password;
        set
        {
            if(ReferenceEquals(password, value)) return;
            password?.Dispose();
            password = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public ICommand AcceptCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand Closed { get; }
}
