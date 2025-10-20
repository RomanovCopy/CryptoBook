using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Models
{
    public class TitleBarTimeWebAIControlModel: ViewModelBase, ITitleBarTimeWebAIControlModel
	{
        public bool IsAgent { get => isAgent; private set => SetProperty(ref isAgent, value); }
        bool isAgent;

        public TitleBarTimeWebAIControlModel()
        {
            var agent = Properties.Settings.Default.AgentId;
            IsAgent=!string.IsNullOrEmpty(agent);
        }

        public bool CanExecute_NewAgent(object? obj)
        {
            return true;
        }

        public void Execute_NewAgent(object? obj)
        {
            IsAgent = !IsAgent;
            if(IsAgent)
            {

            } else
            {

            }
        }

        public bool CanExecute_Close(object? obj)
        {
            return true;
        }

        public void Execute_Loaded(object? obj)
        {
        }

        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }

        public void Execute_Close(object? obj)
        {
        }

        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }

        public void Execute_Closing(object? obj)
        {
        }

        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }

        public void Execute_Closed(object? obj)
        {
        }

    }
}
