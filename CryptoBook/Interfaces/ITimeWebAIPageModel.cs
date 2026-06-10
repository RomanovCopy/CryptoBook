using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ITimeWebAIPageModel:IModel
    {
        bool CanExecute_PageLoaded(object? obj);
        void Execute_PageLoaded(object? obj);

        bool CanExecute_PageClear(object? obj);
        void Execute_PageClear(object? obj);
    }
}
