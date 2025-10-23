using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ITimeWebAIControlModel: IModel
    {
        string Url { get; set; }

        bool CanExecute_NewAgent(object? obj);
        void Execute_NewAgent(object? obj);
    }
}
