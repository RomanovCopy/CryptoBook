using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IInlineChangeScope: IDisposable
    {
        /// <summary>Отменить все изменения в рамках scope.</summary>
        void Cancel();
    }
}
