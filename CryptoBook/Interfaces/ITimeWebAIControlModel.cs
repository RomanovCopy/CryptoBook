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

        bool CanExecuteNewAgent(object? obj);
        void ExecuteNewAgent(object? obj);
    }
}
