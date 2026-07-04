using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLogger.Interfaces
{
    public interface IKeyboardHookService
    {
        void Start();   // запускает хук
        void Stop();    // останавливает хук и закрывает лог
    }
}
