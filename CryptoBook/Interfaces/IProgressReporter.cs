using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IProgressReporter
    {
        // value [0..1] или null если прогресс не детерминирован (например, удаление каталога рекурсивно)
        void Report(double? value, string? currentInfo = null);
    }
}
