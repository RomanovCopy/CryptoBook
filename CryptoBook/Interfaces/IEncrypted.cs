using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IEncrypted
    {
        ICommand EncryptingKeyCommand{  get; }
        ICommand DecryptCommand{ get; }
        ICommand EncryptCommand { get; }
    }
}
