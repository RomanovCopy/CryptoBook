using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    /// <summary>
    /// Состояние защищённого файла.
    /// </summary>
    internal enum SecureFileState
    {
        NotEncrypted,
        Encrypted,
        EncryptedWithInvalidPasswordOrDamaged,
        InvalidFormat
    }
}
