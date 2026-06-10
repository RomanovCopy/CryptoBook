using CryptoBook.Interfaces;

using System.Windows.Input;


namespace CryptoBook.Infrastructure
{
    [Serializable]
    public class RelayCommand: ICommand, IRaiseCanExecuteChanged
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool> _canExecute;
        private readonly object? _fixedParameter;

        private event EventHandler? _canExecuteChangedInternal;

        // Подпишем внешних слушателей ещё и на CommandManager.RequerySuggested:
        public event EventHandler? CanExecuteChanged
        {
            add
            {
                _canExecuteChangedInternal += value;
                CommandManager.RequerySuggested += value;
            }
            remove
            {
                _canExecuteChangedInternal -= value;
                CommandManager.RequerySuggested -= value;
            }
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null, object? fixedParameter = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (_ => true);
            _fixedParameter = fixedParameter;
        }

        public void Execute(object? parameter)
        {
            var actual = parameter ?? _fixedParameter;
            _execute(actual);
        }

        public bool CanExecute(object? parameter)
        {
            var actual = parameter ?? _fixedParameter;
            try
            { return _canExecute(actual); } catch { return false; } // чтобы не уронить UI при ошибке в предикате
        }

        public void RaiseCanExecuteChanged()
        {
            var handler = _canExecuteChangedInternal;
            if(handler == null)
                return;

            if(System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                handler(this, EventArgs.Empty);
            } else
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => handler(this, EventArgs.Empty));
            }
        }
    }


}
