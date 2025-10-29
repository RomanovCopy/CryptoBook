using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Services
{
    public class CommandService:ICommandService
    {
        private readonly ConcurrentDictionary<CommandKey, ICommand> _commands = new();

        public void Register(CommandKey key, ICommand command)
        {
            _commands[key] = command;
        }

        public ICommand? GetCommand(CommandKey key)
        {
            return _commands.TryGetValue(key, out var cmd) ? cmd : null;
        }
    }
}
