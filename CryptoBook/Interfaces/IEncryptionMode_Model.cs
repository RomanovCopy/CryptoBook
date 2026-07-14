using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IEncryptionMode_Model: IModel,IWindowOptions, IWindowWithId
    {
        public string Title { get; }
        public string MessageMode { get; }
        public string MessageModeTop { get; }
        public string MessageModeBottom { get; }
        public string Path { get; }
        public string WarningMessage { get; }
    }
}
