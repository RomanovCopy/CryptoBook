using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CryptoBook.Interfaces
{
    public interface IStockIconService:IService
    {
        ImageSource GetStockIcon(DTO.SHSTOCKICONID id, bool small = true);
        void ClearCache();
    }
}
