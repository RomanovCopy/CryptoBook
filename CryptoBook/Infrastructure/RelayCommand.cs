using System.Windows.Input;


namespace CryptoBook.Infrastructure
{
    [Serializable]
    public class RelayCommand: ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?> _canExecute;
        private readonly object? _fixedParameter;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null,
            object? fixedParameter = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (_ => true);
            _fixedParameter = fixedParameter;
        }

        public void Execute(object? parameter)
        {
            var actualParameter = parameter ?? _fixedParameter;
            _execute.Invoke(actualParameter);
        }

        public bool CanExecute(object? parameter)
        {
            var actualParameter = parameter ?? _fixedParameter;
            return _canExecute.Invoke(actualParameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }


}
