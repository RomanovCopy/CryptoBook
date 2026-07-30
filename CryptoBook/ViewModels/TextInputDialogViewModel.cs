using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class TextInputDialogViewModel:
        ViewModelBase,
        IWindowWithId,
        IDialogResult<string?>
    {
        private string _value;

        public TextInputDialogViewModel(IWindowContext context)
        {
            DialogTitle = context.Get<string>("title");
            Prompt = context.Get<string>("prompt");
            AcceptButtonText = context.Get<string>("acceptButtonText");
            _value = context.Get<string>("initialValue");
        }

        public event EventHandler<bool?>? CloseRequested;

        public Guid WindowId { get; } = Guid.NewGuid();
        public string DialogTitle { get; }
        public string Prompt { get; }
        public string AcceptButtonText { get; }
        public string? Result { get; private set; }

        public string Value
        {
            get => _value;
            set
            {
                if(SetProperty(ref _value, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand AcceptCommand => _acceptCommand
            ??= new RelayCommand(Accept, _ => !string.IsNullOrWhiteSpace(Value));
        private RelayCommand? _acceptCommand;

        public ICommand CancelCommand => _cancelCommand
            ??= new RelayCommand(Cancel);
        private RelayCommand? _cancelCommand;

        private void Accept(object? parameter)
        {
            Result = Value.Trim();
            CloseRequested?.Invoke(this, true);
        }

        private void Cancel(object? parameter)
        {
            Result = null;
            CloseRequested?.Invoke(this, false);
        }
    }
}
