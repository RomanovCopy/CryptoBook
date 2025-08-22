using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IListService
    {
        void ToggleBulleted();
        void ToggleNumbered(int startIndex = 1);
        void ClearLists();                 // снять список с выделения
        bool CanToggle { get; }            // есть ли параграфы под выделением
    }
}
