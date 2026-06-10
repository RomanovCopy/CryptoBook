using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IColumnLayoutStore:IService
    {
        bool TryLoad(string viewId, out IReadOnlyList<double> ratios);
        void Save(string viewId, IReadOnlyList<double> ratios);
    }
}
