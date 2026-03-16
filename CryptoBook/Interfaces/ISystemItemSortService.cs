using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ISystemItemSortService:IService
    {
        IComparer<ISystemItem> GetComparer(SystemItemSortType sortType, int direction);
        void Sort(IList<ISystemItem> items, SystemItemSortType sortType, int direction);
    }
}
