using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Runtime.Serialization.Formatters.Binary;// бинарная сериализация


namespace CryptoBook.Infrastructure
{
    [Serializable]
    public class RelayCommand: ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (_ => true);
        }

        public void Execute(object? parameter)
        {
            if(_execute == null)
                throw new InvalidOperationException("Execute command is not initialized");
            _execute.Invoke(parameter);
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute.Invoke(parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add
            {
                if(value != null)
                {
                    CommandManager.RequerySuggested += value;
                }
            }
            remove
            {
                if(value != null)
                {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }
    }

}
